namespace Portal.Infrastructure.Entities;

public class PayslipEmailLog
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public string SentByUserId { get; set; } = string.Empty;
    public string SentToEmail { get; set; } = string.Empty;
    public bool IsSignatureIncluded { get; set; }
    public DateTime SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
