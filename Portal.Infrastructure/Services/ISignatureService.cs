using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Receipt;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for signature upload, management, and retrieval.
/// </summary>
public interface ISignatureService
{
    Task<ServiceResult<SignatureViewModel>> UploadAsync(int businessId, string userId, string label, string? position, string fileName, string contentType, Stream fileStream);
    Task<List<SignatureViewModel>> GetAllForBusinessAsync(int businessId);
    Task<List<SignatureViewModel>> GetAllIncludingInactiveAsync(int businessId);
    Task<SignatureViewModel?> GetByIdAsync(int id, int businessId);
    Task<SignatureViewModel?> GetDefaultAsync(int businessId);
    Task<ServiceResult> SetDefaultAsync(int id, int businessId);
    Task<ServiceResult> DeactivateAsync(int id, int businessId);
    Task<ServiceResult> ReactivateAsync(int id, int businessId);
    Task<ServiceResult> UpdateLabelAsync(int id, int businessId, string label, string? position = null);
    Task<Stream?> GetImageStreamAsync(int id, int businessId);

    /// <summary>
    /// One-time backfill: measures on-disk file sizes for legacy signatures that have no recorded
    /// size (FileSizeBytes = 0) and writes them. Idempotent. Returns (updated, skipped) counts.
    /// </summary>
    Task<(int Updated, int Skipped)> BackfillFileSizesAsync();
}
