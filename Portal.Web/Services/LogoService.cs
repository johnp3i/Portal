using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Manages business logo uploads, validation, and deletion.
/// Logos are stored under the private file-storage root at
/// {FileStorage:BasePath}/{businessId}/logos/{guid}{ext} (tenant-isolated, redeploy-safe) and
/// served through the public-but-unguessable streaming route /logo/{businessId}/{filename}
/// (LogoController.Image). The stored PublicUrl points at that route.
/// </summary>
public class LogoService : ILogoService
{
    private readonly BusinessLogoRepository _logoRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogoService> _logger;

    private const int MaxLogosPerBusiness = 20;
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/svg+xml",
        "image/webp"
    };

    public LogoService(BusinessLogoRepository logoRepository, IConfiguration configuration, ILogger<LogoService> logger)
    {
        _logoRepository = logoRepository;
        _configuration = configuration;
        _logger = logger;
    }

    private string BasePath =>
        _configuration["FileStorage:BasePath"]
        ?? throw new InvalidOperationException("FileStorage:BasePath is not configured in appsettings.");

    /// <summary>Physical folder holding a business's logo files.</summary>
    private string LogoDirectory(int businessId) =>
        Path.Combine(BasePath, businessId.ToString(), "logos");

    public async Task<BusinessLogo> UploadAsync(int businessId, IFormFile file, string displayName)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is required.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException("Invalid file format. Accepted formats: PNG, JPG, SVG, WebP.");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File size exceeds the maximum allowed size of 2MB.");

        var currentCount = await _logoRepository.GetCountByBusinessIdAsync(businessId);
        if (currentCount >= MaxLogosPerBusiness)
            throw new InvalidOperationException($"Maximum of {MaxLogosPerBusiness} logos per business reached.");

        var uploadsPath = LogoDirectory(businessId);
        Directory.CreateDirectory(uploadsPath);

        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, uniqueFileName);

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var logo = new BusinessLogo
            {
                BusinessId = businessId,
                DisplayName = displayName,
                FileName = uniqueFileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                PublicUrl = $"/logo/{businessId}/{uniqueFileName}",
                CreatedAtUtc = DateTime.UtcNow
            };

            await _logoRepository.InsertAsync(logo);

            _logger.LogInformation("Logo uploaded for business {BusinessId}: {FileName}", businessId, uniqueFileName);

            return logo;
        }
        catch (Exception)
        {
            // Clean up file if DB insert fails
            if (File.Exists(filePath))
                File.Delete(filePath);
            throw;
        }
    }

    public async Task<List<BusinessLogo>> GetByBusinessIdAsync(int businessId)
    {
        return await _logoRepository.GetByBusinessIdAsync(businessId);
    }

    public async Task DeleteAsync(int logoId, int businessId)
    {
        var logo = await _logoRepository.GetByIdAsync(logoId);
        if (logo == null || logo.BusinessId != businessId)
            throw new InvalidOperationException("Logo not found.");

        var filePath = Path.Combine(LogoDirectory(businessId), logo.FileName);

        await _logoRepository.DeleteAsync(logoId);

        if (File.Exists(filePath))
            File.Delete(filePath);

        _logger.LogInformation("Logo deleted for business {BusinessId}: {FileName}", businessId, logo.FileName);
    }

    public async Task SetPrimaryAsync(int logoId, int businessId)
    {
        var logo = await _logoRepository.GetByIdAsync(logoId);
        if (logo == null || logo.BusinessId != businessId)
            throw new InvalidOperationException("Logo not found.");

        await _logoRepository.SetPrimaryAsync(logoId, businessId);

        _logger.LogInformation("Logo {LogoId} set as primary for business {BusinessId}", logoId, businessId);
    }
}
