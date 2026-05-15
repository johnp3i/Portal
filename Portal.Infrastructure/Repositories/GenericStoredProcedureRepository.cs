using Microsoft.EntityFrameworkCore;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Base class for all repositories. Provides unified async execution of raw SQL queries
/// and stored procedures with strong typing via generic constraint.
/// </summary>
public class GenericStoredProcedureRepository<T> where T : class
{
    protected readonly DbContext _context;

    public GenericStoredProcedureRepository(DbContext context)
    {
        _context = context;
    }

    protected async Task<List<T>> ExecuteStoredProcedure(string sqlQuery, params object[] parameters)
        => await _context.Set<T>().FromSqlRaw(sqlQuery, parameters).ToListAsync();

    protected async Task<T?> ExecuteSingleRecordStoredProcedure(string sqlQuery, params object[] parameters)
        => (await _context.Set<T>().FromSqlRaw(sqlQuery, parameters).ToListAsync()).FirstOrDefault();
}
