namespace Portal.Infrastructure.Models;

public class LimitCheckResult
{
    public bool HasWarning { get; set; }
    public List<LimitWarning> Warnings { get; set; } = new();
}
