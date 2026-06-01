namespace Portal.Web.Services;

/// <summary>
/// Represents the outcome of a public registration attempt.
/// </summary>
public class RegistrationResult
{
    public bool Succeeded { get; set; }
    public string? UserId { get; set; }
    public List<string> Errors { get; set; } = new();

    public static RegistrationResult Success(string userId) => new()
    {
        Succeeded = true,
        UserId = userId
    };

    public static RegistrationResult Failure(params string[] errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };

    public static RegistrationResult Failure(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };
}
