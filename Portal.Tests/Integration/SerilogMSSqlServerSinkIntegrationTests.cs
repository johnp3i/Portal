using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Integration test: Verifies that log entries written via Serilog's MSSqlServer sink
/// reach the database with correct custom columns populated (CorrelationId, UserId,
/// BusinessId, SourceContext, MachineName).
///
/// This test requires a real SQL Server instance. It is marked with [Trait("Category", "Integration")]
/// so it can be excluded in CI environments without a database.
///
/// **Validates: Requirements 3.1, 3.4, 5.4**
/// </summary>
[Trait("Category", "Integration")]
public class SerilogMSSqlServerSinkIntegrationTests : IAsyncLifetime
{
    private static readonly string TestConnectionString =
        Environment.GetEnvironmentVariable("PORTAL_TEST_LOGGING_CONNECTION")
        ?? "Server=127.0.0.1;Database=Portal.Logging.Tests;User ID=sa;Password=onlyme1986;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("PORTAL_TEST_SQL_CONNECTION")
        ?? "Server=127.0.0.1;Database=master;User ID=sa;Password=onlyme1986;TrustServerCertificate=True;Connect Timeout=5";

    private const string TestTableName = "Logs";
    private bool _databaseAvailable;

    public async Task InitializeAsync()
    {
        _databaseAvailable = await TryCreateTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_databaseAvailable)
        {
            await CleanupTestTableAsync();
        }
    }

    [Fact]
    public async Task LogEntry_ReachesDatabase_WithCorrectColumns()
    {
        Skip.If(!_databaseAvailable, "SQL Server is not available for integration testing.");

        // Arrange
        var testCorrelationId = Guid.NewGuid().ToString();
        var testUserId = "test-user-" + Guid.NewGuid().ToString("N")[..8];
        var testBusinessId = 42;
        var testMessage = $"Integration test log entry {Guid.NewGuid()}";

        var logger = CreateTestLogger();

        try
        {
            // Act: Push enriched properties into LogContext and write a log entry
            using (LogContext.PushProperty("CorrelationId", testCorrelationId))
            using (LogContext.PushProperty("UserId", testUserId))
            using (LogContext.PushProperty("BusinessId", testBusinessId))
            {
                logger.Information(testMessage);
            }
        }
        finally
        {
            // Flush the sink to ensure the batch is written
            await CloseAndFlushAsync(logger);
        }
        // Assert: Query the Logs table to verify the entry was written with correct columns
        await using var connection = new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT Logs.Id, Logs.Message, Logs.Level, Logs.TimeStamp, 
                     Logs.CorrelationId, Logs.UserId, Logs.BusinessId, 
                     Logs.SourceContext, Logs.MachineName
              FROM [dbo].[Logs]
              WHERE Logs.Message = @Message", connection);
        command.Parameters.AddWithValue("@Message", testMessage);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected at least one log entry in the database.");

        // Verify custom columns are populated
        Assert.Equal(testCorrelationId, reader["CorrelationId"]?.ToString());
        Assert.Equal(testUserId, reader["UserId"]?.ToString());
        Assert.Equal(testBusinessId, Convert.ToInt32(reader["BusinessId"]));
        Assert.Equal(Environment.MachineName, reader["MachineName"]?.ToString());
        Assert.Equal("Information", reader["Level"]?.ToString());
        Assert.NotEqual(DBNull.Value, reader["TimeStamp"]);

        // SourceContext is set by the ForContext call in CreateTestLogger
        var sourceContext = reader["SourceContext"]?.ToString();
        Assert.False(string.IsNullOrEmpty(sourceContext), "SourceContext should be populated.");
    }

    #region Test Infrastructure

    private static Serilog.ILogger CreateTestLogger()
    {
        var columnOptions = new ColumnOptions();
        columnOptions.Store.Remove(StandardColumn.Properties);
        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new SqlColumn { ColumnName = "CorrelationId", DataType = SqlDbType.NVarChar, DataLength = 128, AllowNull = true },
            new SqlColumn { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 450, AllowNull = true },
            new SqlColumn { ColumnName = "BusinessId", DataType = SqlDbType.Int, AllowNull = true },
            new SqlColumn { ColumnName = "SourceContext", DataType = SqlDbType.NVarChar, DataLength = 512, AllowNull = true },
            new SqlColumn { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 512, AllowNull = true },
            new SqlColumn { ColumnName = "MachineName", DataType = SqlDbType.NVarChar, DataLength = 128, AllowNull = true }
        };
        columnOptions.TimeStamp.ConvertToUtc = true;

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.MSSqlServer(
                connectionString: TestConnectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = TestTableName,
                    SchemaName = "dbo",
                    AutoCreateSqlTable = true,
                    BatchPostingLimit = 1,
                    BatchPeriod = TimeSpan.FromMilliseconds(100)
                },
                columnOptions: columnOptions,
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger()
            .ForContext<SerilogMSSqlServerSinkIntegrationTests>();
    }

    private static async Task CloseAndFlushAsync(Serilog.ILogger logger)
    {
        // Dispose the logger if it's a disposable (Logger implements IDisposable)
        if (logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
        // Give a small buffer for the batch to complete
        await Task.Delay(500);
    }

    private static async Task<bool> TryCreateTestDatabaseAsync()
    {
        try
        {
            await using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'Portal.Logging.Tests')
                  BEGIN
                      CREATE DATABASE [Portal.Logging.Tests];
                  END", connection);
            await command.ExecuteNonQueryAsync();

            return true;
        }
        catch (SqlException)
        {
            // SQL Server is not available — skip the test gracefully
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task CleanupTestTableAsync()
    {
        try
        {
            await using var connection = new SqlConnection(TestConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(
                "IF OBJECT_ID('[dbo].[Logs]', 'U') IS NOT NULL DELETE FROM [dbo].[Logs]", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup — don't fail the test on cleanup errors
        }
    }

    #endregion
}
