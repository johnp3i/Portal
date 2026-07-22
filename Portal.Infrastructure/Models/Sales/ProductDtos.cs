namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a sales product in a list.
/// </summary>
public class SalesProductListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Request model for creating a sales product.
/// </summary>
public class CreateSalesProductRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// Request model for updating a sales product.
/// </summary>
public class UpdateSalesProductRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// A simple product option for dropdowns.
/// </summary>
public class SalesProductOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
