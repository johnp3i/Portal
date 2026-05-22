namespace Portal.Infrastructure.Models;

public class SharedLinkViewModel
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentReference { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public string ShareToken { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
