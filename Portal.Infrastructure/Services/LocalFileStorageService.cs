using Microsoft.Extensions.Configuration;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Local filesystem implementation of IFileStorageService.
/// Stores files under the configured upload path: {basePath}/{businessId}/{entityType}/{entityId}/{guid}_{originalFileName}.
/// The base path is read from configuration key "FileStorage:BasePath".
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath is not configured in appsettings.");
    }

    public async Task<string> UploadAsync(int businessId, string entityType, int entityId, string originalFileName, Stream fileStream)
    {
        try
        {
            var uniqueFileName = $"{Guid.NewGuid():N}_{originalFileName}";
            var relativePath = Path.Combine(businessId.ToString(), entityType, entityId.ToString(), uniqueFileName);
            var fullPath = Path.Combine(_basePath, relativePath);

            var directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);

            using var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(outputStream);

            // Return forward-slash path for consistent storage regardless of OS
            return relativePath.Replace('\\', '/');
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to upload file to storage.", ex);
        }
    }

    public Task<Stream> DownloadAsync(string storagePath)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The requested file was not found in storage.");
            }

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to download file from storage.", ex);
        }
    }

    public Task DeleteAsync(string storagePath)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to delete file from storage.", ex);
        }
    }

    public Task<bool> ExistsAsync(string storagePath)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));
            return Task.FromResult(File.Exists(fullPath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to check file existence in storage.", ex);
        }
    }
}
