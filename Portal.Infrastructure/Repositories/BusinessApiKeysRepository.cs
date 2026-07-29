using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for managing encrypted Stripe API keys per business.
/// </summary>
public class BusinessApiKeysRepository
{
    private readonly PortalDbContext _context;

    public BusinessApiKeysRepository(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<List<BusinessApiKey>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            return await _context.BusinessApiKeys
                .Where(k => k.BusinessId == businessId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<BusinessApiKey?> GetByBusinessIdAndKeyTypeAsync(int businessId, string keyType)
    {
        try
        {
            return await _context.BusinessApiKeys
                .FirstOrDefaultAsync(k => k.BusinessId == businessId && k.KeyType == keyType);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpsertAsync(BusinessApiKey entity)
    {
        try
        {
            var existing = await _context.BusinessApiKeys
                .FirstOrDefaultAsync(k => k.BusinessId == entity.BusinessId && k.KeyType == entity.KeyType);

            if (existing != null)
            {
                existing.EncryptedValue = entity.EncryptedValue;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                entity.CreatedAtUtc = DateTime.UtcNow;
                _context.BusinessApiKeys.Add(entity);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DeleteAllByBusinessIdAsync(int businessId)
    {
        try
        {
            var keys = await _context.BusinessApiKeys
                .Where(k => k.BusinessId == businessId)
                .ToListAsync();

            if (keys.Any())
            {
                _context.BusinessApiKeys.RemoveRange(keys);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
