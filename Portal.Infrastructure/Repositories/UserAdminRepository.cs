using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Identity;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for user administration operations against [membership].[UserBusiness]
/// and [membership].[UserBusinessPermission] tables.
/// Backed by MembershipDbContext.
/// </summary>
public class UserAdminRepository : GenericStoredProcedureRepository<UserBusiness>
{
    public UserAdminRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Returns a paginated list of UserBusiness records for a given business,
    /// with optional search (case-insensitive full name or email) and active status filter.
    /// </summary>
    public async Task<(List<UserBusiness> Items, int TotalCount)> GetUsersPagedAsync(
        int businessId, string? searchTerm, bool? isActive, int skip, int take)
    {
        try
        {
            var query = _context.Set<UserBusiness>()
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == businessId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(ub =>
                    (ub.User.FirstName + " " + ub.User.LastName).ToLower().Contains(term) ||
                    ub.User.Email!.ToLower().Contains(term));
            }

            if (isActive.HasValue)
            {
                query = query.Where(ub => ub.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(ub => ub.User.LastName)
                .ThenBy(ub => ub.User.FirstName)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (items, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a single UserBusiness record by its primary key, including the User navigation property.
    /// Returns null if not found.
    /// </summary>
    public async Task<UserBusiness?> GetByIdAsync(int userBusinessId)
    {
        try
        {
            return await _context.Set<UserBusiness>()
                .Include(ub => ub.User)
                .FirstOrDefaultAsync(ub => ub.Id == userBusinessId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets IsActive = false and DeactivatedAtUtc = @deactivatedAtUtc for the given UserBusiness record.
    /// </summary>
    public async Task DeactivateAsync(int userBusinessId, DateTime deactivatedAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [membership].[UserBusiness]
                SET
                    [IsActive] = 0,
                    [DeactivatedAtUtc] = @DeactivatedAtUtc
                WHERE [membership].[UserBusiness].[Id] = @UserBusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@UserBusinessId", userBusinessId),
                new SqlParameter("@DeactivatedAtUtc", deactivatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets IsActive = true and DeactivatedAtUtc = NULL for the given UserBusiness record.
    /// </summary>
    public async Task ReactivateAsync(int userBusinessId)
    {
        try
        {
            const string query = @"
                UPDATE [membership].[UserBusiness]
                SET
                    [IsActive] = 1,
                    [DeactivatedAtUtc] = NULL
                WHERE [membership].[UserBusiness].[Id] = @UserBusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@UserBusinessId", userBusinessId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns all UserBusinessPermission records for the given UserBusiness.
    /// </summary>
    public async Task<List<UserBusinessPermission>> GetPermissionsAsync(int userBusinessId)
    {
        try
        {
            return await _context.Set<UserBusinessPermission>()
                .Where(p => p.UserBusinessId == userBusinessId)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new UserBusinessPermission record if none exists for the given
    /// userBusinessId + module combination, otherwise updates the existing record.
    /// </summary>
    public async Task UpsertPermissionAsync(
        int userBusinessId, string module, string accessLevel, bool isActive, DateTime? deactivatedAtUtc)
    {
        try
        {
            var existing = await _context.Set<UserBusinessPermission>()
                .FirstOrDefaultAsync(p => p.UserBusinessId == userBusinessId && p.Module == module);

            if (existing == null)
            {
                const string insertQuery = @"
                    INSERT INTO [membership].[UserBusinessPermission]
                        ([UserBusinessId], [Module], [AccessLevel], [IsActive], [DeactivatedAtUtc], [CreatedAtUtc])
                    VALUES
                        (@UserBusinessId, @Module, @AccessLevel, @IsActive, @DeactivatedAtUtc, @CreatedAtUtc)";

                await _context.Database.ExecuteSqlRawAsync(insertQuery,
                    new SqlParameter("@UserBusinessId", userBusinessId),
                    new SqlParameter("@Module", module),
                    new SqlParameter("@AccessLevel", accessLevel),
                    new SqlParameter("@IsActive", isActive),
                    new SqlParameter("@DeactivatedAtUtc", deactivatedAtUtc ?? (object)DBNull.Value),
                    new SqlParameter("@CreatedAtUtc", DateTime.UtcNow)
                );
            }
            else
            {
                const string updateQuery = @"
                    UPDATE [membership].[UserBusinessPermission]
                    SET
                        [AccessLevel] = @AccessLevel,
                        [IsActive] = @IsActive,
                        [DeactivatedAtUtc] = @DeactivatedAtUtc
                    WHERE [membership].[UserBusinessPermission].[Id] = @Id";

                await _context.Database.ExecuteSqlRawAsync(updateQuery,
                    new SqlParameter("@Id", existing.Id),
                    new SqlParameter("@AccessLevel", accessLevel),
                    new SqlParameter("@IsActive", isActive),
                    new SqlParameter("@DeactivatedAtUtc", deactivatedAtUtc ?? (object)DBNull.Value)
                );
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
