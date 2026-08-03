namespace Portal.Infrastructure.Entities;

/// <summary>
/// A platform-wide expense category template managed by SuperAdmin.
/// Business users can import these into their own ExpenseCategory records.
/// Schema: [purchase].ExpenseCategoryTemplate
/// </summary>
public class ExpenseCategoryTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
