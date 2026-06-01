namespace Portal.Web.Services;

/// <summary>
/// Verifies reCAPTCHA tokens against Google's siteverify API.
/// </summary>
public interface IReCaptchaService
{
    /// <summary>
    /// Verifies the given reCAPTCHA token and returns the verification result.
    /// </summary>
    Task<ReCaptchaResult> VerifyAsync(string token);
}
