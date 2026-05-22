using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Parses CSV content into validated CsvPurchaseRowDto rows,
/// performing name matching against active suppliers and expense categories.
/// </summary>
public interface ICsvImportService
{
    /// <summary>
    /// Parses CSV content and validates each row, resolving supplier and expense category names
    /// to their IDs via case-insensitive matching against the provided active records.
    /// </summary>
    /// <param name="csvContent">Raw CSV file content as a string (including header row).</param>
    /// <param name="activeSuppliers">Active suppliers for the current tenant.</param>
    /// <param name="activeExpenseCategories">Active expense categories for the current tenant.</param>
    /// <returns>A CsvImportResult containing the parsed rows or an error message if the file is rejected.</returns>
    CsvImportResult ParseAndValidate(string csvContent, List<Supplier> activeSuppliers, List<ExpenseCategory> activeExpenseCategories);
}
