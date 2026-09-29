using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Guards against the "required column 'X' was not present in the results" regression that once
/// broke Billing: a scalar property is added to an entity but a raw-SQL read query's SELECT list
/// is not updated, so EF Core cannot materialize the entity at runtime.
///
/// These repositories use SQL-Server-specific raw SQL ([schema].[table], MERGE, OUTPUT INSERTED),
/// which neither EF InMemory nor SQLite can execute faithfully — so we cannot exercise the real
/// queries against a fake provider. Instead we take the authoritative list of mapped scalar
/// columns straight from the EF model and assert every one is named in the repository's SELECT
/// column constant. That is exactly the completeness check EF performs when materialising, minus
/// the live database — it catches a forgotten column the moment the model and the SQL drift apart.
///
/// The SELECT column lists were extracted into public constants on each repository precisely so
/// this test can read them without a live connection.
/// </summary>
public class RepositorySqlColumnCoverageTests : IDisposable
{
    private readonly PortalDbContext _context;

    public RepositorySqlColumnCoverageTests()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(1);
        _context = new PortalDbContext(options, tenantMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// The column names EF Core expects to be present when materialising <typeparamref name="T"/>
    /// from a raw SQL result — every mapped scalar property that is not a shadow / navigation.
    /// </summary>
    private IReadOnlyList<string> MappedScalarColumns<T>() where T : class
    {
        var entity = _context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped in PortalDbContext.");

        return entity.GetProperties()
            .Where(p => !p.IsShadowProperty())
            .Select(p => p.GetColumnName())
            .ToList();
    }

    private static void AssertCoversAllColumns(string selectColumns, IReadOnlyList<string> requiredColumns, string queryName)
    {
        var missing = requiredColumns
            .Where(col => !selectColumns.Contains($"[{col}]", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{queryName} is missing mapped column(s) [{string.Join(", ", missing)}] from its SELECT list. " +
            "EF Core would throw \"The required column was not present in the results\" at runtime. " +
            "Add the column(s) to the repository's SelectColumns constant.");
    }

    [Fact]
    public void PlanRepository_SelectColumns_CoversEveryMappedPlanColumn()
    {
        AssertCoversAllColumns(
            PlanRepository.SelectColumns,
            MappedScalarColumns<Plan>(),
            "PlanRepository read query");
    }

    [Fact]
    public void SignatureRepository_SelectColumns_CoversEveryMappedSignatureColumn()
    {
        AssertCoversAllColumns(
            SignatureRepository.SelectColumns,
            MappedScalarColumns<Signature>(),
            "SignatureRepository read query");
    }

    [Fact]
    public void OrphanedFileStatusType_SelectColumns_CoversEveryMappedColumn()
    {
        AssertCoversAllColumns(
            OrphanedFileCleanupRepository.StatusTypeSelectColumns,
            MappedScalarColumns<OrphanedFileStatusType>(),
            "OrphanedFileCleanupRepository.GetStatusTypesAsync");
    }

    [Fact]
    public void OrphanedFileDeletionLog_SelectColumns_CoversEveryMappedColumn()
    {
        AssertCoversAllColumns(
            OrphanedFileCleanupRepository.DeletionLogSelectColumns,
            MappedScalarColumns<OrphanedFileDeletionLog>(),
            "OrphanedFileCleanupRepository.GetDeletionLogAsync");
    }

    [Fact]
    public void EveryMappedEntity_HasNonEmptyColumnSet_SanityCheck()
    {
        // If the model ever stops mapping these entities the coverage tests above become vacuous,
        // so assert the column sets are actually populated.
        Assert.NotEmpty(MappedScalarColumns<Plan>());
        Assert.NotEmpty(MappedScalarColumns<Signature>());
        Assert.NotEmpty(MappedScalarColumns<OrphanedFileStatusType>());
        Assert.NotEmpty(MappedScalarColumns<OrphanedFileDeletionLog>());
    }
}
