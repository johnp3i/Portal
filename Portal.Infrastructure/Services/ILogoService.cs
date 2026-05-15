using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service for managing business logo uploads and retrieval.
/// </summary>
public interface ILogoService
{
    Task<BusinessLogo> UploadAsync(int businessId, IFormFile file, string displayName);
    Task<List<BusinessLogo>> GetByBusinessIdAsync(int businessId);
    Task DeleteAsync(int logoId, int businessId);
    Task SetPrimaryAsync(int logoId, int businessId);
}
