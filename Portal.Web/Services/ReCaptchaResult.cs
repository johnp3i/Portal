using System.Text.Json.Serialization;

namespace Portal.Web.Services;

/// <summary>
/// Represents the result of a reCAPTCHA token verification.
/// </summary>
public class ReCaptchaResult
{
    public bool Success { get; set; }
    public double Score { get; set; }
    public string? Action { get; set; }
    public string[]? ErrorCodes { get; set; }
}

/// <summary>
/// Internal model for deserializing the Google reCAPTCHA siteverify API response.
/// </summary>
internal class GoogleReCaptchaResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; set; }
}
