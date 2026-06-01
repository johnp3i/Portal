using System.Net.Http.Json;

namespace Portal.Web.Services;

/// <summary>
/// Verifies reCAPTCHA tokens by posting to Google's siteverify API.
/// </summary>
public class ReCaptchaService : IReCaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly ILogger<ReCaptchaService> _logger;

    public ReCaptchaService(HttpClient httpClient, IConfiguration configuration, ILogger<ReCaptchaService> logger)
    {
        _httpClient = httpClient;
        _secretKey = configuration["ReCaptcha:SecretKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<ReCaptchaResult> VerifyAsync(string token)
    {
        var response = await _httpClient.PostAsync(
            "https://www.google.com/recaptcha/api/siteverify",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secretKey,
                ["response"] = token
            }));

        var json = await response.Content.ReadFromJsonAsync<GoogleReCaptchaResponse>();

        return new ReCaptchaResult
        {
            Success = json?.Success ?? false,
            Score = json?.Score ?? 0,
            Action = json?.Action,
            ErrorCodes = json?.ErrorCodes
        };
    }
}
