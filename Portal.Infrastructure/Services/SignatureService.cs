using Microsoft.Extensions.Configuration;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Receipt;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages digital signatures: upload, retrieval, default selection, and deactivation.
/// Files stored under {BasePath}/signatures/{businessId}/{guid}_{fileName}.
/// </summary>
public class SignatureService : ISignatureService
{
    private readonly SignatureRepository _signatureRepository;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/svg+xml"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".svg"
    };

    private const int MaxSignaturesPerBusiness = 10;

    // Magic bytes for PNG files
    private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47 };

    public SignatureService(SignatureRepository signatureRepository, IConfiguration configuration)
    {
        _signatureRepository = signatureRepository;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SignatureViewModel>> UploadAsync(
        int businessId, string userId, string label, string? position, string fileName, string contentType, Stream fileStream)
    {
        try
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            if (!AllowedExtensions.Contains(extension))
                return ServiceResult<SignatureViewModel>.Fail("Only PNG and SVG files are allowed for signatures.");

            if (!AllowedContentTypes.Contains(contentType))
                return ServiceResult<SignatureViewModel>.Fail("Invalid content type. Accepted: PNG, SVG.");

            if (string.IsNullOrWhiteSpace(label))
                return ServiceResult<SignatureViewModel>.Fail("Signature label is required.");

            // Check upload limit
            var existing = await _signatureRepository.GetByBusinessIdAsync(businessId);
            if (existing.Count >= MaxSignaturesPerBusiness)
                return ServiceResult<SignatureViewModel>.Fail($"Maximum of {MaxSignaturesPerBusiness} active signatures allowed.");

            // Validate magic bytes for PNG
            if (extension == ".png")
            {
                var headerBytes = new byte[4];
                var bytesRead = await fileStream.ReadAsync(headerBytes, 0, 4);
                fileStream.Position = 0; // Reset stream after reading

                if (bytesRead < 4 || !headerBytes.AsSpan(0, 4).SequenceEqual(PngMagicBytes))
                    return ServiceResult<SignatureViewModel>.Fail("File content does not match PNG format.");
            }

            // Sanitize filename — keep only alphanumeric, hyphens, underscores, and extension
            var sanitizedName = SanitizeFileName(fileName);

            // Store file
            var basePath = _configuration["FileStorage:BasePath"] ?? "C:/BusinessPortal/Uploads";
            var relativePath = $"signatures/{businessId}/{Guid.NewGuid():N}_{sanitizedName}";
            var fullPath = Path.Combine(basePath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(fs);
            }

            var entity = new Signature
            {
                BusinessId = businessId,
                Label = label,
                Position = position,
                FileName = sanitizedName,
                ContentType = contentType,
                FilePath = relativePath,
                IsDefault = false,
                IsActive = true,
                UploadedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            var id = await _signatureRepository.InsertAsync(entity);

            return ServiceResult<SignatureViewModel>.Ok(new SignatureViewModel
            {
                Id = id,
                Label = label,
                Position = position,
                FileName = sanitizedName,
                ContentType = contentType,
                IsDefault = false,
                IsActive = true,
                CreatedAtUtc = entity.CreatedAtUtc,
                UploadedByDisplayName = userId
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<SignatureViewModel>> GetAllForBusinessAsync(int businessId)
    {
        try
        {
            var signatures = await _signatureRepository.GetByBusinessIdAsync(businessId);
            return signatures.Select(s => new SignatureViewModel
            {
                Id = s.Id,
                Label = s.Label,
                Position = s.Position,
                FileName = s.FileName,
                ContentType = s.ContentType,
                IsDefault = s.IsDefault,
                IsActive = s.IsActive,
                CreatedAtUtc = s.CreatedAtUtc,
                UploadedByDisplayName = s.UploadedByUserId
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SignatureViewModel?> GetDefaultAsync(int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetDefaultAsync(businessId);
            if (sig == null) return null;

            return new SignatureViewModel
            {
                Id = sig.Id,
                Label = sig.Label,
                Position = sig.Position,
                FileName = sig.FileName,
                ContentType = sig.ContentType,
                IsDefault = sig.IsDefault,
                IsActive = sig.IsActive,
                CreatedAtUtc = sig.CreatedAtUtc,
                UploadedByDisplayName = sig.UploadedByUserId
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SetDefaultAsync(int id, int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return ServiceResult.Fail("Signature not found.");
            if (!sig.IsActive) return ServiceResult.Fail("Cannot set an inactive signature as default.");

            await _signatureRepository.SetDefaultAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeactivateAsync(int id, int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return ServiceResult.Fail("Signature not found.");

            await _signatureRepository.DeactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReactivateAsync(int id, int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return ServiceResult.Fail("Signature not found.");

            await _signatureRepository.ReactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateLabelAsync(int id, int businessId, string label, string? position = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(label))
                return ServiceResult.Fail("Label is required.");

            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return ServiceResult.Fail("Signature not found.");

            await _signatureRepository.UpdateLabelAsync(id, businessId, label.Trim(), position?.Trim());
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> GetImageStreamAsync(int id, int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return null;

            var basePath = _configuration["FileStorage:BasePath"] ?? "C:/BusinessPortal/Uploads";
            var fullPath = Path.Combine(basePath, sig.FilePath);

            if (!File.Exists(fullPath)) return null;

            return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SignatureViewModel?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            var sig = await _signatureRepository.GetByIdAsync(id, businessId);
            if (sig == null) return null;

            return new SignatureViewModel
            {
                Id = sig.Id,
                Label = sig.Label,
                Position = sig.Position,
                FileName = sig.FileName,
                ContentType = sig.ContentType,
                IsDefault = sig.IsDefault,
                IsActive = sig.IsActive,
                CreatedAtUtc = sig.CreatedAtUtc,
                UploadedByDisplayName = sig.UploadedByUserId
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<SignatureViewModel>> GetAllIncludingInactiveAsync(int businessId)
    {
        try
        {
            // GetByBusinessIdAsync only returns active. Query all via repository's GetByIdAsync pattern.
            var allSignatures = await _signatureRepository.GetAllByBusinessIdAsync(businessId);
            return allSignatures.Select(s => new SignatureViewModel
            {
                Id = s.Id,
                Label = s.Label,
                Position = s.Position,
                FileName = s.FileName,
                ContentType = s.ContentType,
                IsDefault = s.IsDefault,
                IsActive = s.IsActive,
                CreatedAtUtc = s.CreatedAtUtc,
                UploadedByDisplayName = s.UploadedByUserId
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        // Keep only alphanumeric, hyphens, underscores
        var sanitized = new string(nameWithoutExt.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "signature";
        return sanitized + extension;
    }
}
