namespace Portal.Infrastructure.Models;

/// <summary>
/// Lightweight Id + Name pair for populating dropdowns in views.
/// </summary>
public class SelectListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
