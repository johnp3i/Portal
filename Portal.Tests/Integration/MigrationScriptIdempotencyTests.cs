using Microsoft.Data.SqlClient;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Smoke test: Verifies the logging database migration script is idempotent
/// and creates the correct schema (columns, types, and indexes).
///
/// Requires a running SQL Server instance. If the database is unreachable,
/// the test skips gracefully.
///
/// **Validates: Requirements 7.2, 7.3, 7.4**
/// </summary>
[Trait("Category", "Integration")]
public class MigrationScriptIdempotencyTests : IAsyncLifetime
{
    // Use a unique test database name to avoid conflicts
    private const string TestDatabaseName = "Portal.Logging.Test";

    // Connection string to the SQL Server instance (master database)
    // Uses local SQL Server with integrated security; override via environment variable
    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("PORTAL_TEST_SQL_CONNECTION")
        ?? "Server=127.0.0.1;Database=master;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Connect Timeout=5";

    private string _migrationScript = string.Empty;
    private bool _canConnect;

    public async Task InitializeAsync()
    {
        // Load the migration script from the file system
        var scriptPath = FindMigrationScriptPath();
        if (scriptPath == null)
        {
            _canConnect = false;
            return;
        }

        _migrationScript = await File.ReadAllTextAsync(scriptPath);

        // Replace the real database name with our test database name
        _migrationScript = _migrationScript.Replace("Portal.Logging", TestDatabaseName);

        // Test connectivity
        _canConnect = await CanConnectToSqlServer();
    }

    public async Task DisposeAsync()
    {
        if (!_canConnect) return;

        // Clean up: drop the test database
        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();

            var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $@"
                IF EXISTS (SELECT 1 FROM sys.databases WHERE [name] = '{TestDatabaseName}')
                BEGIN
                    ALTER DATABASE [{TestDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{TestDatabaseName}];
                END";
            await dropCommand.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [SkippableFact]
    public async Task MigrationScript_ExecutedTwice_NoErrors()
    {
        Skip.If(!_canConnect, "SQL Server is not available. Skipping integration test.");

        // Act: Execute the migration script the first time
        await ExecuteMigrationScript();

        // Act: Execute the migration script a second time (idempotency check)
        var exception = await Record.ExceptionAsync(() => ExecuteMigrationScript());

        // Assert: No errors on second execution
        Assert.Null(exception);
    }

    [SkippableFact]
    public async Task MigrationScript_CreatesAllExpectedColumns_WithCorrectTypes()
    {
        Skip.If(!_canConnect, "SQL Server is not available. Skipping integration test.");

        // Arrange: Execute the migration script
        await ExecuteMigrationScript();

        // Act: Query INFORMATION_SCHEMA.COLUMNS for the Logs table
        var columns = await GetTableColumns();

        // Assert: Verify all expected columns exist with correct types
        var expectedColumns = new Dictionary<string, (string DataType, string? MaxLength, string IsNullable)>
        {
            ["Id"] = ("bigint", null, "NO"),
            ["Message"] = ("nvarchar", "-1", "NO"),
            ["MessageTemplate"] = ("nvarchar", "-1", "YES"),
            ["Level"] = ("nvarchar", "128", "NO"),
            ["TimeStamp"] = ("datetime2", null, "NO"),
            ["Exception"] = ("nvarchar", "-1", "YES"),
            ["CorrelationId"] = ("nvarchar", "128", "YES"),
            ["UserId"] = ("nvarchar", "450", "YES"),
            ["BusinessId"] = ("int", null, "YES"),
            ["SourceContext"] = ("nvarchar", "512", "YES"),
            ["RequestPath"] = ("nvarchar", "512", "YES"),
            ["MachineName"] = ("nvarchar", "128", "YES"),
        };

        Assert.Equal(expectedColumns.Count, columns.Count);

        foreach (var (columnName, (expectedType, expectedMaxLength, expectedNullable)) in expectedColumns)
        {
            Assert.True(columns.ContainsKey(columnName), $"Column '{columnName}' not found in Logs table.");

            var (actualType, actualMaxLength, actualNullable) = columns[columnName];
            Assert.Equal(expectedType, actualType);
            Assert.Equal(expectedNullable, actualNullable);

            if (expectedMaxLength != null)
            {
                Assert.Equal(expectedMaxLength, actualMaxLength);
            }
        }
    }

    [SkippableFact]
    public async Task MigrationScript_CreatesAllExpectedIndexes()
    {
        Skip.If(!_canConnect, "SQL Server is not available. Skipping integration test.");

        // Arrange: Execute the migration script
        await ExecuteMigrationScript();

        // Act: Query sys.indexes for the Logs table
        var indexes = await GetTableIndexes();

        // Assert: Verify all 3 non-clustered indexes exist
        var expectedIndexes = new[] { "IX_Logs_TimeStamp", "IX_Logs_Level", "IX_Logs_BusinessId" };

        foreach (var expectedIndex in expectedIndexes)
        {
            Assert.True(
                indexes.Any(idx => idx.Name == expectedIndex && idx.Type == "NONCLUSTERED"),
                $"Non-clustered index '{expectedIndex}' not found on Logs table.");
        }

        // Also verify the clustered primary key exists
        Assert.True(
            indexes.Any(idx => idx.Name == "PK_Logs" && idx.Type == "CLUSTERED"),
            "Clustered primary key 'PK_Logs' not found on Logs table.");
    }

    #region Helper Methods

    private static string? FindMigrationScriptPath()
    {
        // Walk up from the test assembly location to find the repository root
        var directory = AppDomain.CurrentDomain.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(directory, "Portal.Database", "Migrations", "Logging", "001_CreateLoggingDatabase.sql");
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(directory);
            if (parent == null) break;
            directory = parent.FullName;
        }

        return null;
    }

    private static async Task<bool> CanConnectToSqlServer()
    {
        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ExecuteMigrationScript()
    {
        // Split on GO statements (SQL Server batch separator)
        var batches = SplitOnGo(_migrationScript);

        using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch))
                continue;

            using var command = connection.CreateCommand();
            command.CommandText = trimmedBatch;
            await command.ExecuteNonQueryAsync();

            // After USE statement, switch to the test database
            if (trimmedBatch.Contains($"USE [{TestDatabaseName}]", StringComparison.OrdinalIgnoreCase))
            {
                await connection.ChangeDatabaseAsync(TestDatabaseName);
            }
        }
    }

    private static string[] SplitOnGo(string script)
    {
        // Split on "GO" that appears on its own line (standard SQL Server batch separator)
        return System.Text.RegularExpressions.Regex.Split(
            script,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task<Dictionary<string, (string DataType, string MaxLength, string IsNullable)>> GetTableColumns()
    {
        var testDbConnectionString = MasterConnectionString.Replace("Database=master", $"Database={TestDatabaseName}");
        using var connection = new SqlConnection(testDbConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                CAST(COALESCE(CHARACTER_MAXIMUM_LENGTH, -99) AS VARCHAR(10)) AS MAX_LENGTH,
                IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Logs'
            ORDER BY ORDINAL_POSITION";

        var columns = new Dictionary<string, (string DataType, string MaxLength, string IsNullable)>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var maxLength = reader.GetString(2);
            var isNullable = reader.GetString(3);

            // Normalize: -99 means no character length (numeric/datetime types)
            if (maxLength == "-99") maxLength = null!;

            columns[name] = (dataType, maxLength, isNullable);
        }

        return columns;
    }

    private async Task<List<(string Name, string Type)>> GetTableIndexes()
    {
        var testDbConnectionString = MasterConnectionString.Replace("Database=master", $"Database={TestDatabaseName}");
        using var connection = new SqlConnection(testDbConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                sys.indexes.[name] AS IndexName,
                CASE sys.indexes.type
                    WHEN 1 THEN 'CLUSTERED'
                    WHEN 2 THEN 'NONCLUSTERED'
                    ELSE 'OTHER'
                END AS IndexType
            FROM sys.indexes
            INNER JOIN sys.tables ON sys.indexes.[object_id] = sys.tables.[object_id]
            WHERE sys.tables.[name] = 'Logs'
              AND sys.indexes.[name] IS NOT NULL
            ORDER BY sys.indexes.[name]";

        var indexes = new List<(string Name, string Type)>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add((reader.GetString(0), reader.GetString(1)));
        }

        return indexes;
    }

    #endregion
}
