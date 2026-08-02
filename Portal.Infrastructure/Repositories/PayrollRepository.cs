using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for all payroll module data access operations.
/// Handles Department, Employee, EarningType, DeductionType, DeductionRateHistory,
/// PayslipPeriod, Payslip, PayslipEarningLine, PayslipDeductionLine,
/// EmployeeDefaultEarnings, and PayslipEmailLog entities.
/// Schema: [payroll]
/// </summary>
public class PayrollRepository : GenericStoredProcedureRepository<PayslipPeriod>
{
    public PayrollRepository(DbContext context) : base(context) { }

    #region Department Methods

    /// <summary>
    /// Gets all departments for a business, ordered by name.
    /// </summary>
    public async Task<List<Department>> GetDepartmentsByBusinessAsync(int businessId)
    {
        try
        {
            var results = new List<Department>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [BusinessId], [Name], [IsActive], [CreatedAtUtc]
                    FROM [payroll].[Department]
                    WHERE [payroll].[Department].[BusinessId] = @BusinessId
                    ORDER BY [payroll].[Department].[Name]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDepartment(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single department by Id scoped to a business.
    /// </summary>
    public async Task<Department?> GetDepartmentByIdAsync(int id, int businessId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [BusinessId], [Name], [IsActive], [CreatedAtUtc]
                    FROM [payroll].[Department]
                    WHERE [payroll].[Department].[Id] = @Id
                      AND [payroll].[Department].[BusinessId] = @BusinessId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Id", id));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapDepartment(reader);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new department and returns the generated Id.
    /// </summary>
    public async Task<int> InsertDepartmentAsync(Department entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[Department]
                    ([BusinessId], [Name])
                VALUES
                    (@BusinessId, @Name);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing department's Name.
    /// </summary>
    public async Task UpdateDepartmentAsync(Department entity)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[Department]
                SET [Name] = @Name
                WHERE [payroll].[Department].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a department name already exists for a business. Optionally excludes a specific Id.
    /// </summary>
    public async Task<bool> DepartmentNameExistsAsync(int businessId, string name, int? excludeId)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Name", name)
            };

            var query = @"
                SELECT COUNT(*)
                FROM [payroll].[Department]
                WHERE [payroll].[Department].[BusinessId] = @BusinessId
                  AND [payroll].[Department].[Name] = @Name";

            if (excludeId.HasValue)
            {
                query += " AND [payroll].[Department].[Id] != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", excludeId.Value));
            }

            var result = await _context.Database.SqlQueryRaw<int>(query, parameters.ToArray()).ToListAsync();
            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a department has active employees assigned to it.
    /// </summary>
    public async Task<bool> DepartmentHasActiveEmployeesAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [payroll].[Employee]
                WHERE [payroll].[Employee].[DepartmentId] = @Id
                  AND [payroll].[Employee].[IsActive] = 1";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@Id", id)
            ).ToListAsync();

            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Employee Methods

    /// <summary>
    /// Gets a paginated list of employees with optional filtering by search, department, and active status.
    /// Returns items and total count for pagination.
    /// </summary>
    public async Task<(List<Employee> Items, int TotalCount)> GetEmployeesAsync(
        int businessId, string? search, int? departmentId, bool? isActive, int page, int pageSize)
    {
        try
        {
            int offset = (page - 1) * pageSize;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize)
            };

            var whereClause = "[payroll].[Employee].[BusinessId] = @BusinessId";

            if (!string.IsNullOrEmpty(search))
            {
                whereClause += " AND ([payroll].[Employee].[Name] LIKE @Search OR [payroll].[Employee].[Email] LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            if (departmentId.HasValue)
            {
                whereClause += " AND [payroll].[Employee].[DepartmentId] = @DepartmentId";
                parameters.Add(new SqlParameter("@DepartmentId", departmentId.Value));
            }

            if (isActive.HasValue)
            {
                whereClause += " AND [payroll].[Employee].[IsActive] = @IsActive";
                parameters.Add(new SqlParameter("@IsActive", isActive.Value));
            }

            var query = $@"
                SELECT [payroll].[Employee].[Id],
                       [payroll].[Employee].[BusinessId],
                       [payroll].[Employee].[DepartmentId],
                       [payroll].[Employee].[Name],
                       [payroll].[Employee].[Position],
                       [payroll].[Employee].[SocialInsuranceNumber],
                       [payroll].[Employee].[IdNumber],
                       [payroll].[Employee].[Phone],
                       [payroll].[Employee].[Email],
                       [payroll].[Employee].[StartDate],
                       [payroll].[Employee].[EndDate],
                       [payroll].[Employee].[SalaryTypeId],
                       [payroll].[Employee].[BaseSalary],
                       [payroll].[Employee].[HourlyRate],
                       [payroll].[Employee].[BankAccount],
                       [payroll].[Employee].[IsActive],
                       [payroll].[Employee].[CreatedAtUtc],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [payroll].[Employee]
                WHERE {whereClause}
                ORDER BY [payroll].[Employee].[Name]
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<Employee>();
            int totalCount = 0;
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                foreach (var param in parameters)
                    command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(MapEmployee(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single employee by Id scoped to a business.
    /// </summary>
    public async Task<Employee?> GetEmployeeByIdAsync(int id, int businessId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [BusinessId], [DepartmentId], [Name], [Position],
                           [SocialInsuranceNumber], [IdNumber], [Phone], [Email],
                           [StartDate], [EndDate], [SalaryTypeId], [BaseSalary],
                           [HourlyRate], [BankAccount], [IsActive], [CreatedAtUtc]
                    FROM [payroll].[Employee]
                    WHERE [payroll].[Employee].[Id] = @Id
                      AND [payroll].[Employee].[BusinessId] = @BusinessId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Id", id));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapEmployee(reader);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new employee and returns the generated Id.
    /// </summary>
    public async Task<int> InsertEmployeeAsync(Employee entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[Employee]
                    ([BusinessId], [DepartmentId], [Name], [Position],
                     [SocialInsuranceNumber], [IdNumber], [Phone], [Email],
                     [StartDate], [EndDate], [SalaryTypeId], [BaseSalary],
                     [HourlyRate], [BankAccount], [IsActive])
                VALUES
                    (@BusinessId, @DepartmentId, @Name, @Position,
                     @SocialInsuranceNumber, @IdNumber, @Phone, @Email,
                     @StartDate, @EndDate, @SalaryTypeId, @BaseSalary,
                     @HourlyRate, @BankAccount, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@DepartmentId", entity.DepartmentId ?? (object)DBNull.Value),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Position", entity.Position ?? (object)DBNull.Value),
                new SqlParameter("@SocialInsuranceNumber", entity.SocialInsuranceNumber),
                new SqlParameter("@IdNumber", entity.IdNumber),
                new SqlParameter("@Phone", entity.Phone ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@StartDate", entity.StartDate),
                new SqlParameter("@EndDate", entity.EndDate ?? (object)DBNull.Value),
                new SqlParameter("@SalaryTypeId", entity.SalaryTypeId),
                new SqlParameter("@BaseSalary", entity.BaseSalary),
                new SqlParameter("@HourlyRate", entity.HourlyRate ?? (object)DBNull.Value),
                new SqlParameter("@BankAccount", entity.BankAccount ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates all editable columns of an employee.
    /// </summary>
    public async Task UpdateEmployeeAsync(Employee entity)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[Employee]
                SET [DepartmentId] = @DepartmentId,
                    [Name] = @Name,
                    [Position] = @Position,
                    [SocialInsuranceNumber] = @SocialInsuranceNumber,
                    [IdNumber] = @IdNumber,
                    [Phone] = @Phone,
                    [Email] = @Email,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [SalaryTypeId] = @SalaryTypeId,
                    [BaseSalary] = @BaseSalary,
                    [HourlyRate] = @HourlyRate,
                    [BankAccount] = @BankAccount,
                    [IsActive] = @IsActive
                WHERE [payroll].[Employee].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@DepartmentId", entity.DepartmentId ?? (object)DBNull.Value),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Position", entity.Position ?? (object)DBNull.Value),
                new SqlParameter("@SocialInsuranceNumber", entity.SocialInsuranceNumber),
                new SqlParameter("@IdNumber", entity.IdNumber),
                new SqlParameter("@Phone", entity.Phone ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@StartDate", entity.StartDate),
                new SqlParameter("@EndDate", entity.EndDate ?? (object)DBNull.Value),
                new SqlParameter("@SalaryTypeId", entity.SalaryTypeId),
                new SqlParameter("@BaseSalary", entity.BaseSalary),
                new SqlParameter("@HourlyRate", entity.HourlyRate ?? (object)DBNull.Value),
                new SqlParameter("@BankAccount", entity.BankAccount ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a social insurance number already exists for a business. Optionally excludes a specific Id.
    /// </summary>
    public async Task<bool> SocialInsuranceNumberExistsAsync(int businessId, string sin, int? excludeId)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@SIN", sin)
            };

            var query = @"
                SELECT COUNT(*)
                FROM [payroll].[Employee]
                WHERE [payroll].[Employee].[BusinessId] = @BusinessId
                  AND [payroll].[Employee].[SocialInsuranceNumber] = @SIN";

            if (excludeId.HasValue)
            {
                query += " AND [payroll].[Employee].[Id] != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", excludeId.Value));
            }

            var result = await _context.Database.SqlQueryRaw<int>(query, parameters.ToArray()).ToListAsync();
            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if an ID number already exists for a business. Optionally excludes a specific Id.
    /// </summary>
    public async Task<bool> IdNumberExistsAsync(int businessId, string idNumber, int? excludeId)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@IdNumber", idNumber)
            };

            var query = @"
                SELECT COUNT(*)
                FROM [payroll].[Employee]
                WHERE [payroll].[Employee].[BusinessId] = @BusinessId
                  AND [payroll].[Employee].[IdNumber] = @IdNumber";

            if (excludeId.HasValue)
            {
                query += " AND [payroll].[Employee].[Id] != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", excludeId.Value));
            }

            var result = await _context.Database.SqlQueryRaw<int>(query, parameters.ToArray()).ToListAsync();
            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets active employees for a business that are eligible for a given payroll period.
    /// </summary>
    public async Task<List<Employee>> GetActiveEmployeesForPeriodAsync(int businessId, DateTime periodStart)
    {
        try
        {
            var results = new List<Employee>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [BusinessId], [DepartmentId], [Name], [Position],
                           [SocialInsuranceNumber], [IdNumber], [Phone], [Email],
                           [StartDate], [EndDate], [SalaryTypeId], [BaseSalary],
                           [HourlyRate], [BankAccount], [IsActive], [CreatedAtUtc]
                    FROM [payroll].[Employee]
                    WHERE [payroll].[Employee].[BusinessId] = @BusinessId
                      AND [payroll].[Employee].[IsActive] = 1
                      AND ([payroll].[Employee].[EndDate] IS NULL OR [payroll].[Employee].[EndDate] >= @PeriodStart)
                    ORDER BY [payroll].[Employee].[Name]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@PeriodStart", periodStart));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapEmployee(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region EarningType Methods

    /// <summary>
    /// Gets all earning types ordered by SortOrder.
    /// </summary>
    public async Task<List<EarningType>> GetAllEarningTypesAsync()
    {
        try
        {
            var results = new List<EarningType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Code], [IsActive], [SortOrder], [CreatedAtUtc]
                    FROM [payroll].[EarningType]
                    ORDER BY [payroll].[EarningType].[SortOrder]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapEarningType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new earning type and returns the generated Id.
    /// </summary>
    public async Task<int> InsertEarningTypeAsync(EarningType entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[EarningType]
                    ([Name], [Code], [IsActive], [SortOrder])
                VALUES
                    (@Name, @Code, @IsActive, @SortOrder);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Code", entity.Code),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@SortOrder", entity.SortOrder)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Toggles the IsActive flag of an earning type (flips the bit).
    /// </summary>
    public async Task ToggleEarningTypeAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[EarningType]
                SET [IsActive] = ~[IsActive]
                WHERE [payroll].[EarningType].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region DeductionType Methods

    /// <summary>
    /// Gets all deduction types (templates and business-specific).
    /// </summary>
    public async Task<List<DeductionType>> GetAllDeductionTypesAsync()
    {
        try
        {
            var results = new List<DeductionType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                           [BusinessId], [IsActive], [Country], [IsTemplate], [CreatedAtUtc]
                    FROM [payroll].[DeductionType]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDeductionType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets deduction types for a specific business.
    /// </summary>
    public async Task<List<DeductionType>> GetDeductionTypesByBusinessAsync(int businessId)
    {
        try
        {
            var results = new List<DeductionType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                           [BusinessId], [IsActive], [Country], [IsTemplate], [CreatedAtUtc]
                    FROM [payroll].[DeductionType]
                    WHERE [payroll].[DeductionType].[BusinessId] = @BusinessId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDeductionType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new deduction type and returns the generated Id.
    /// </summary>
    public async Task<int> InsertDeductionTypeAsync(DeductionType entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[DeductionType]
                    ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                     [BusinessId], [IsActive], [Country], [IsTemplate])
                VALUES
                    (@Name, @Code, @IsPercentage, @DeductionCategoryTypeId,
                     @BusinessId, @IsActive, @Country, @IsTemplate);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Code", entity.Code),
                new SqlParameter("@IsPercentage", entity.IsPercentage),
                new SqlParameter("@DeductionCategoryTypeId", entity.DeductionCategoryTypeId),
                new SqlParameter("@BusinessId", entity.BusinessId ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@Country", entity.Country),
                new SqlParameter("@IsTemplate", entity.IsTemplate)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Toggles the IsActive flag of a deduction type (flips the bit).
    /// </summary>
    public async Task ToggleDeductionTypeAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[DeductionType]
                SET [IsActive] = ~[IsActive]
                WHERE [payroll].[DeductionType].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets active deduction types with rates for a business (for calculation engine).
    /// </summary>
    public async Task<List<DeductionType>> GetActiveDeductionsWithRatesAsync(int businessId)
    {
        try
        {
            var results = new List<DeductionType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                           [BusinessId], [IsActive], [Country], [IsTemplate], [CreatedAtUtc]
                    FROM [payroll].[DeductionType]
                    WHERE [payroll].[DeductionType].[BusinessId] = @BusinessId
                      AND [payroll].[DeductionType].[IsActive] = 1";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDeductionType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets template deduction types by country.
    /// </summary>
    public async Task<List<DeductionType>> GetTemplatesByCountryAsync(string country)
    {
        try
        {
            var results = new List<DeductionType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                           [BusinessId], [IsActive], [Country], [IsTemplate], [CreatedAtUtc]
                    FROM [payroll].[DeductionType]
                    WHERE [payroll].[DeductionType].[IsTemplate] = 1
                      AND [payroll].[DeductionType].[Country] = @Country";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Country", country));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDeductionType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a deduction type and its associated rate history records in a single operation.
    /// </summary>
    public async Task InsertDeductionTypeWithRatesAsync(DeductionType type, List<DeductionRateHistory> rates)
    {
        try
        {
            const string insertTypeQuery = @"
                INSERT INTO [payroll].[DeductionType]
                    ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId],
                     [BusinessId], [IsActive], [Country], [IsTemplate])
                VALUES
                    (@Name, @Code, @IsPercentage, @DeductionCategoryTypeId,
                     @BusinessId, @IsActive, @Country, @IsTemplate);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var typeIdResult = await _context.Database.SqlQueryRaw<int>(insertTypeQuery,
                new SqlParameter("@Name", type.Name),
                new SqlParameter("@Code", type.Code),
                new SqlParameter("@IsPercentage", type.IsPercentage),
                new SqlParameter("@DeductionCategoryTypeId", type.DeductionCategoryTypeId),
                new SqlParameter("@BusinessId", type.BusinessId ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", type.IsActive),
                new SqlParameter("@Country", type.Country),
                new SqlParameter("@IsTemplate", type.IsTemplate)
            ).ToListAsync();

            var newTypeId = typeIdResult.FirstOrDefault();

            const string insertRateQuery = @"
                INSERT INTO [payroll].[DeductionRateHistory]
                    ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc])
                VALUES
                    (@DeductionTypeId, @Rate, @EffectiveFromUtc, @EffectiveToUtc)";

            foreach (var rate in rates)
            {
                await _context.Database.ExecuteSqlRawAsync(insertRateQuery,
                    new SqlParameter("@DeductionTypeId", newTypeId),
                    new SqlParameter("@Rate", rate.Rate),
                    new SqlParameter("@EffectiveFromUtc", rate.EffectiveFromUtc),
                    new SqlParameter("@EffectiveToUtc", rate.EffectiveToUtc ?? (object)DBNull.Value));
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region DeductionRateHistory Methods

    /// <summary>
    /// Gets rate history for a deduction type, ordered by EffectiveFromUtc descending.
    /// </summary>
    public async Task<List<DeductionRateHistory>> GetRateHistoryAsync(int deductionTypeId)
    {
        try
        {
            var results = new List<DeductionRateHistory>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc], [CreatedAtUtc]
                    FROM [payroll].[DeductionRateHistory]
                    WHERE [payroll].[DeductionRateHistory].[DeductionTypeId] = @DeductionTypeId
                    ORDER BY [payroll].[DeductionRateHistory].[EffectiveFromUtc] DESC";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@DeductionTypeId", deductionTypeId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapDeductionRateHistory(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new deduction rate history record and returns the generated Id.
    /// </summary>
    public async Task<int> InsertRateHistoryAsync(DeductionRateHistory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[DeductionRateHistory]
                    ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc])
                VALUES
                    (@DeductionTypeId, @Rate, @EffectiveFromUtc, @EffectiveToUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@DeductionTypeId", entity.DeductionTypeId),
                new SqlParameter("@Rate", entity.Rate),
                new SqlParameter("@EffectiveFromUtc", entity.EffectiveFromUtc),
                new SqlParameter("@EffectiveToUtc", entity.EffectiveToUtc ?? (object)DBNull.Value)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Closes the current open rate by setting EffectiveToUtc WHERE EffectiveToUtc IS NULL.
    /// </summary>
    public async Task CloseCurrentRateAsync(int deductionTypeId, DateTime effectiveToUtc)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[DeductionRateHistory]
                SET [EffectiveToUtc] = @EffectiveToUtc
                WHERE [payroll].[DeductionRateHistory].[DeductionTypeId] = @DeductionTypeId
                  AND [payroll].[DeductionRateHistory].[EffectiveToUtc] IS NULL";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@DeductionTypeId", deductionTypeId),
                new SqlParameter("@EffectiveToUtc", effectiveToUtc));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region PayslipPeriod Methods

    /// <summary>
    /// Gets all payslip periods for a business, ordered by Year DESC then Month DESC.
    /// </summary>
    public async Task<List<PayslipPeriod>> GetPeriodsByBusinessAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Year], [Month], [PayslipStatusTypeId], [ProcessedAtUtc], [CreatedAtUtc]
                FROM [payroll].[PayslipPeriod]
                WHERE [payroll].[PayslipPeriod].[BusinessId] = @BusinessId
                ORDER BY [payroll].[PayslipPeriod].[Year] DESC, [payroll].[PayslipPeriod].[Month] DESC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single payslip period by Id scoped to a business.
    /// </summary>
    public async Task<PayslipPeriod?> GetPeriodByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Year], [Month], [PayslipStatusTypeId], [ProcessedAtUtc], [CreatedAtUtc]
                FROM [payroll].[PayslipPeriod]
                WHERE [payroll].[PayslipPeriod].[Id] = @Id
                  AND [payroll].[PayslipPeriod].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new payslip period and returns the generated Id.
    /// </summary>
    public async Task<int> InsertPeriodAsync(PayslipPeriod entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[PayslipPeriod]
                    ([BusinessId], [Year], [Month], [PayslipStatusTypeId])
                VALUES
                    (@BusinessId, @Year, @Month, @PayslipStatusTypeId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Year", entity.Year),
                new SqlParameter("@Month", entity.Month),
                new SqlParameter("@PayslipStatusTypeId", entity.PayslipStatusTypeId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the status and optional processed timestamp of a payslip period.
    /// </summary>
    public async Task UpdatePeriodStatusAsync(int id, byte statusTypeId, DateTime? processedAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[PayslipPeriod]
                SET [PayslipStatusTypeId] = @PayslipStatusTypeId,
                    [ProcessedAtUtc] = @ProcessedAtUtc
                WHERE [payroll].[PayslipPeriod].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@PayslipStatusTypeId", statusTypeId),
                new SqlParameter("@ProcessedAtUtc", processedAtUtc ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a period already exists for a business, year, and month.
    /// </summary>
    public async Task<bool> PeriodExistsAsync(int businessId, int year, int month)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [payroll].[PayslipPeriod]
                WHERE [payroll].[PayslipPeriod].[BusinessId] = @BusinessId
                  AND [payroll].[PayslipPeriod].[Year] = @Year
                  AND [payroll].[PayslipPeriod].[Month] = @Month";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Year", year),
                new SqlParameter("@Month", month)
            ).ToListAsync();

            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Payslip Methods

    /// <summary>
    /// Inserts a new payslip and returns the generated Id.
    /// </summary>
    public async Task<int> InsertPayslipAsync(Payslip entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[Payslip]
                    ([EmployeeId], [PayslipPeriodId], [TotalEarnings],
                     [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions],
                     [ManagerNotes], [PayslipStatusTypeId])
                VALUES
                    (@EmployeeId, @PayslipPeriodId, @TotalEarnings,
                     @TotalEmployeeDeductions, @NetSalary, @TotalEmployerContributions,
                     @ManagerNotes, @PayslipStatusTypeId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@EmployeeId", entity.EmployeeId),
                new SqlParameter("@PayslipPeriodId", entity.PayslipPeriodId),
                new SqlParameter("@TotalEarnings", entity.TotalEarnings),
                new SqlParameter("@TotalEmployeeDeductions", entity.TotalEmployeeDeductions),
                new SqlParameter("@NetSalary", entity.NetSalary),
                new SqlParameter("@TotalEmployerContributions", entity.TotalEmployerContributions),
                new SqlParameter("@ManagerNotes", entity.ManagerNotes ?? (object)DBNull.Value),
                new SqlParameter("@PayslipStatusTypeId", entity.PayslipStatusTypeId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all payslips for a given period.
    /// </summary>
    public async Task<List<Payslip>> GetPayslipsByPeriodAsync(int periodId)
    {
        try
        {
            var results = new List<Payslip>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [EmployeeId], [PayslipPeriodId], [TotalEarnings],
                           [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions],
                           [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc]
                    FROM [payroll].[Payslip]
                    WHERE [payroll].[Payslip].[PayslipPeriodId] = @PeriodId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@PeriodId", periodId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapPayslip(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single payslip by Id with business ownership validation via PayslipPeriod join.
    /// </summary>
    public async Task<Payslip?> GetPayslipDetailAsync(int id, int businessId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [payroll].[Payslip].[Id],
                           [payroll].[Payslip].[EmployeeId],
                           [payroll].[Payslip].[PayslipPeriodId],
                           [payroll].[Payslip].[TotalEarnings],
                           [payroll].[Payslip].[TotalEmployeeDeductions],
                           [payroll].[Payslip].[NetSalary],
                           [payroll].[Payslip].[TotalEmployerContributions],
                           [payroll].[Payslip].[ManagerNotes],
                           [payroll].[Payslip].[PayslipStatusTypeId],
                           [payroll].[Payslip].[CreatedAtUtc]
                    FROM [payroll].[Payslip]
                    INNER JOIN [payroll].[PayslipPeriod]
                        ON [payroll].[Payslip].[PayslipPeriodId] = [payroll].[PayslipPeriod].[Id]
                    WHERE [payroll].[Payslip].[Id] = @Id
                      AND [payroll].[PayslipPeriod].[BusinessId] = @BusinessId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Id", id));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapPayslip(reader);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the financial totals of a payslip.
    /// </summary>
    public async Task UpdatePayslipTotalsAsync(Payslip entity)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[Payslip]
                SET [TotalEarnings] = @TotalEarnings,
                    [TotalEmployeeDeductions] = @TotalEmployeeDeductions,
                    [NetSalary] = @NetSalary,
                    [TotalEmployerContributions] = @TotalEmployerContributions
                WHERE [payroll].[Payslip].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@TotalEarnings", entity.TotalEarnings),
                new SqlParameter("@TotalEmployeeDeductions", entity.TotalEmployeeDeductions),
                new SqlParameter("@NetSalary", entity.NetSalary),
                new SqlParameter("@TotalEmployerContributions", entity.TotalEmployerContributions));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the manager notes of a payslip.
    /// </summary>
    public async Task UpdateManagerNotesAsync(int payslipId, string? notes)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[Payslip]
                SET [ManagerNotes] = @ManagerNotes
                WHERE [payroll].[Payslip].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", payslipId),
                new SqlParameter("@ManagerNotes", notes ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region EarningLine Methods

    /// <summary>
    /// Inserts a new earning line for a payslip.
    /// </summary>
    public async Task InsertEarningLineAsync(PayslipEarningLine entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[PayslipEarningLine]
                    ([PayslipId], [EarningTypeId], [Description], [Amount],
                     [OvertimeMultiplier], [OvertimeHours])
                VALUES
                    (@PayslipId, @EarningTypeId, @Description, @Amount,
                     @OvertimeMultiplier, @OvertimeHours)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", entity.PayslipId),
                new SqlParameter("@EarningTypeId", entity.EarningTypeId),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Amount", entity.Amount),
                new SqlParameter("@OvertimeMultiplier", entity.OvertimeMultiplier ?? (object)DBNull.Value),
                new SqlParameter("@OvertimeHours", entity.OvertimeHours ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes all earning lines for a payslip.
    /// </summary>
    public async Task DeleteEarningLinesByPayslipAsync(int payslipId)
    {
        try
        {
            const string query = @"
                DELETE FROM [payroll].[PayslipEarningLine]
                WHERE [payroll].[PayslipEarningLine].[PayslipId] = @PayslipId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", payslipId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all earning lines for a payslip.
    /// </summary>
    public async Task<List<PayslipEarningLine>> GetEarningLinesByPayslipAsync(int payslipId)
    {
        try
        {
            var results = new List<PayslipEarningLine>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [PayslipId], [EarningTypeId], [Description],
                           [Amount], [OvertimeMultiplier], [OvertimeHours], [CreatedAtUtc]
                    FROM [payroll].[PayslipEarningLine]
                    WHERE [payroll].[PayslipEarningLine].[PayslipId] = @PayslipId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@PayslipId", payslipId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapPayslipEarningLine(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region DeductionLine Methods

    /// <summary>
    /// Inserts a new deduction line for a payslip.
    /// </summary>
    public async Task InsertDeductionLineAsync(PayslipDeductionLine entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[PayslipDeductionLine]
                    ([PayslipId], [DeductionTypeId], [BaseAmount], [Rate],
                     [CalculatedAmount], [DeductionCategoryTypeId], [DeductionRateHistoryId])
                VALUES
                    (@PayslipId, @DeductionTypeId, @BaseAmount, @Rate,
                     @CalculatedAmount, @DeductionCategoryTypeId, @DeductionRateHistoryId)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", entity.PayslipId),
                new SqlParameter("@DeductionTypeId", entity.DeductionTypeId),
                new SqlParameter("@BaseAmount", entity.BaseAmount),
                new SqlParameter("@Rate", entity.Rate),
                new SqlParameter("@CalculatedAmount", entity.CalculatedAmount),
                new SqlParameter("@DeductionCategoryTypeId", entity.DeductionCategoryTypeId),
                new SqlParameter("@DeductionRateHistoryId", entity.DeductionRateHistoryId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes all deduction lines for a payslip.
    /// </summary>
    public async Task DeleteDeductionLinesByPayslipAsync(int payslipId)
    {
        try
        {
            const string query = @"
                DELETE FROM [payroll].[PayslipDeductionLine]
                WHERE [payroll].[PayslipDeductionLine].[PayslipId] = @PayslipId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", payslipId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all deduction lines for a payslip.
    /// </summary>
    public async Task<List<PayslipDeductionLine>> GetDeductionLinesByPayslipAsync(int payslipId)
    {
        try
        {
            var results = new List<PayslipDeductionLine>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [PayslipId], [DeductionTypeId], [BaseAmount], [Rate],
                           [CalculatedAmount], [DeductionCategoryTypeId], [DeductionRateHistoryId], [CreatedAtUtc]
                    FROM [payroll].[PayslipDeductionLine]
                    WHERE [payroll].[PayslipDeductionLine].[PayslipId] = @PayslipId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@PayslipId", payslipId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapPayslipDeductionLine(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region EmployeeDefaultEarnings Methods

    /// <summary>
    /// Gets all default earnings for an employee.
    /// </summary>
    public async Task<List<EmployeeDefaultEarnings>> GetDefaultEarningsByEmployeeAsync(int employeeId)
    {
        try
        {
            var results = new List<EmployeeDefaultEarnings>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [EmployeeId], [EarningTypeId], [Description],
                           [Amount], [OvertimeMultiplier], [OvertimeHours], [CreatedAtUtc]
                    FROM [payroll].[EmployeeDefaultEarnings]
                    WHERE [payroll].[EmployeeDefaultEarnings].[EmployeeId] = @EmployeeId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@EmployeeId", employeeId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapEmployeeDefaultEarnings(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new default earning and returns the generated Id.
    /// </summary>
    public async Task<int> InsertDefaultEarningAsync(EmployeeDefaultEarnings entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[EmployeeDefaultEarnings]
                    ([EmployeeId], [EarningTypeId], [Description], [Amount],
                     [OvertimeMultiplier], [OvertimeHours])
                VALUES
                    (@EmployeeId, @EarningTypeId, @Description, @Amount,
                     @OvertimeMultiplier, @OvertimeHours);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@EmployeeId", entity.EmployeeId),
                new SqlParameter("@EarningTypeId", entity.EarningTypeId),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Amount", entity.Amount ?? (object)DBNull.Value),
                new SqlParameter("@OvertimeMultiplier", entity.OvertimeMultiplier ?? (object)DBNull.Value),
                new SqlParameter("@OvertimeHours", entity.OvertimeHours ?? (object)DBNull.Value)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing default earning.
    /// </summary>
    public async Task UpdateDefaultEarningAsync(EmployeeDefaultEarnings entity)
    {
        try
        {
            const string query = @"
                UPDATE [payroll].[EmployeeDefaultEarnings]
                SET [EarningTypeId] = @EarningTypeId,
                    [Description] = @Description,
                    [Amount] = @Amount,
                    [OvertimeMultiplier] = @OvertimeMultiplier,
                    [OvertimeHours] = @OvertimeHours
                WHERE [payroll].[EmployeeDefaultEarnings].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@EarningTypeId", entity.EarningTypeId),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Amount", entity.Amount ?? (object)DBNull.Value),
                new SqlParameter("@OvertimeMultiplier", entity.OvertimeMultiplier ?? (object)DBNull.Value),
                new SqlParameter("@OvertimeHours", entity.OvertimeHours ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes a default earning by Id.
    /// </summary>
    public async Task DeleteDefaultEarningAsync(int id)
    {
        try
        {
            const string query = @"
                DELETE FROM [payroll].[EmployeeDefaultEarnings]
                WHERE [payroll].[EmployeeDefaultEarnings].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region EmailLog Methods

    /// <summary>
    /// Inserts a new payslip email log entry.
    /// </summary>
    public async Task InsertEmailLogAsync(PayslipEmailLog entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[PayslipEmailLog]
                    ([PayslipId], [SentByUserId], [SentToEmail], [IsSignatureIncluded], [SentAtUtc])
                VALUES
                    (@PayslipId, @SentByUserId, @SentToEmail, @IsSignatureIncluded, @SentAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", entity.PayslipId),
                new SqlParameter("@SentByUserId", entity.SentByUserId),
                new SqlParameter("@SentToEmail", entity.SentToEmail),
                new SqlParameter("@IsSignatureIncluded", entity.IsSignatureIncluded),
                new SqlParameter("@SentAtUtc", entity.SentAtUtc));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all email log entries for a payslip.
    /// </summary>
    public async Task<List<PayslipEmailLog>> GetEmailLogsByPayslipAsync(int payslipId)
    {
        try
        {
            var results = new List<PayslipEmailLog>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [PayslipId], [SentByUserId], [SentToEmail],
                           [IsSignatureIncluded], [SentAtUtc], [CreatedAtUtc]
                    FROM [payroll].[PayslipEmailLog]
                    WHERE [payroll].[PayslipEmailLog].[PayslipId] = @PayslipId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@PayslipId", payslipId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapPayslipEmailLog(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Private Mapping Methods

    private static Department MapDepartment(DbDataReader reader)
    {
        return new Department
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static Employee MapEmployee(DbDataReader reader)
    {
        return new Employee
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            DepartmentId = reader.IsDBNull(reader.GetOrdinal("DepartmentId")) ? null : reader.GetInt32(reader.GetOrdinal("DepartmentId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
            SocialInsuranceNumber = reader.GetString(reader.GetOrdinal("SocialInsuranceNumber")),
            IdNumber = reader.GetString(reader.GetOrdinal("IdNumber")),
            Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? null : reader.GetString(reader.GetOrdinal("Phone")),
            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
            StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("EndDate")),
            SalaryTypeId = reader.GetByte(reader.GetOrdinal("SalaryTypeId")),
            BaseSalary = reader.GetDecimal(reader.GetOrdinal("BaseSalary")),
            HourlyRate = reader.IsDBNull(reader.GetOrdinal("HourlyRate")) ? null : reader.GetDecimal(reader.GetOrdinal("HourlyRate")),
            BankAccount = reader.IsDBNull(reader.GetOrdinal("BankAccount")) ? null : reader.GetString(reader.GetOrdinal("BankAccount")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static EarningType MapEarningType(DbDataReader reader)
    {
        return new EarningType
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Code = reader.GetString(reader.GetOrdinal("Code")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static DeductionType MapDeductionType(DbDataReader reader)
    {
        return new DeductionType
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Code = reader.GetString(reader.GetOrdinal("Code")),
            IsPercentage = reader.GetBoolean(reader.GetOrdinal("IsPercentage")),
            DeductionCategoryTypeId = reader.GetByte(reader.GetOrdinal("DeductionCategoryTypeId")),
            BusinessId = reader.IsDBNull(reader.GetOrdinal("BusinessId")) ? null : reader.GetInt32(reader.GetOrdinal("BusinessId")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            Country = reader.GetString(reader.GetOrdinal("Country")),
            IsTemplate = reader.GetBoolean(reader.GetOrdinal("IsTemplate")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static DeductionRateHistory MapDeductionRateHistory(DbDataReader reader)
    {
        return new DeductionRateHistory
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            DeductionTypeId = reader.GetInt32(reader.GetOrdinal("DeductionTypeId")),
            Rate = reader.GetDecimal(reader.GetOrdinal("Rate")),
            EffectiveFromUtc = reader.GetDateTime(reader.GetOrdinal("EffectiveFromUtc")),
            EffectiveToUtc = reader.IsDBNull(reader.GetOrdinal("EffectiveToUtc")) ? null : reader.GetDateTime(reader.GetOrdinal("EffectiveToUtc")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static EmployeeDefaultEarnings MapEmployeeDefaultEarnings(DbDataReader reader)
    {
        return new EmployeeDefaultEarnings
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            EarningTypeId = reader.GetInt32(reader.GetOrdinal("EarningTypeId")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? null : reader.GetDecimal(reader.GetOrdinal("Amount")),
            OvertimeMultiplier = reader.IsDBNull(reader.GetOrdinal("OvertimeMultiplier")) ? null : reader.GetDecimal(reader.GetOrdinal("OvertimeMultiplier")),
            OvertimeHours = reader.IsDBNull(reader.GetOrdinal("OvertimeHours")) ? null : reader.GetDecimal(reader.GetOrdinal("OvertimeHours")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static Payslip MapPayslip(DbDataReader reader)
    {
        return new Payslip
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            PayslipPeriodId = reader.GetInt32(reader.GetOrdinal("PayslipPeriodId")),
            TotalEarnings = reader.GetDecimal(reader.GetOrdinal("TotalEarnings")),
            TotalEmployeeDeductions = reader.GetDecimal(reader.GetOrdinal("TotalEmployeeDeductions")),
            NetSalary = reader.GetDecimal(reader.GetOrdinal("NetSalary")),
            TotalEmployerContributions = reader.GetDecimal(reader.GetOrdinal("TotalEmployerContributions")),
            ManagerNotes = reader.IsDBNull(reader.GetOrdinal("ManagerNotes")) ? null : reader.GetString(reader.GetOrdinal("ManagerNotes")),
            PayslipStatusTypeId = reader.GetByte(reader.GetOrdinal("PayslipStatusTypeId")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static PayslipEarningLine MapPayslipEarningLine(DbDataReader reader)
    {
        return new PayslipEarningLine
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            PayslipId = reader.GetInt32(reader.GetOrdinal("PayslipId")),
            EarningTypeId = reader.GetInt32(reader.GetOrdinal("EarningTypeId")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            OvertimeMultiplier = reader.IsDBNull(reader.GetOrdinal("OvertimeMultiplier")) ? null : reader.GetDecimal(reader.GetOrdinal("OvertimeMultiplier")),
            OvertimeHours = reader.IsDBNull(reader.GetOrdinal("OvertimeHours")) ? null : reader.GetDecimal(reader.GetOrdinal("OvertimeHours")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static PayslipDeductionLine MapPayslipDeductionLine(DbDataReader reader)
    {
        return new PayslipDeductionLine
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            PayslipId = reader.GetInt32(reader.GetOrdinal("PayslipId")),
            DeductionTypeId = reader.GetInt32(reader.GetOrdinal("DeductionTypeId")),
            BaseAmount = reader.GetDecimal(reader.GetOrdinal("BaseAmount")),
            Rate = reader.GetDecimal(reader.GetOrdinal("Rate")),
            CalculatedAmount = reader.GetDecimal(reader.GetOrdinal("CalculatedAmount")),
            DeductionCategoryTypeId = reader.GetByte(reader.GetOrdinal("DeductionCategoryTypeId")),
            DeductionRateHistoryId = reader.GetInt32(reader.GetOrdinal("DeductionRateHistoryId")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static PayslipEmailLog MapPayslipEmailLog(DbDataReader reader)
    {
        return new PayslipEmailLog
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            PayslipId = reader.GetInt32(reader.GetOrdinal("PayslipId")),
            SentByUserId = reader.GetString(reader.GetOrdinal("SentByUserId")),
            SentToEmail = reader.GetString(reader.GetOrdinal("SentToEmail")),
            IsSignatureIncluded = reader.GetBoolean(reader.GetOrdinal("IsSignatureIncluded")),
            SentAtUtc = reader.GetDateTime(reader.GetOrdinal("SentAtUtc")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    #endregion
}
