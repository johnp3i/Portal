namespace Portal.Infrastructure.Services;

/// <summary>
/// Abstracts file storage operations, enabling local filesystem in development
/// and Azure Blob Storage in production without changing business logic.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file and returns the relative storage path.
    /// </summary>
    Task<string> UploadAsync(int businessId, string entityType, int entityId, string originalFileName, Stream fileStream);

    /// <summary>
    /// Downloads a file by its storage path, returning a readable stream.
    /// </summary>
    Task<Stream> DownloadAsync(string storagePath);

    /// <summary>
    /// Deletes a file from storage (used for cleanup scenarios, not soft-delete).
    /// </summary>
    Task DeleteAsync(string storagePath);

    /// <summary>
    /// Checks whether a file exists at the given storage path.
    /// </summary>
    Task<bool> ExistsAsync(string storagePath);
}
