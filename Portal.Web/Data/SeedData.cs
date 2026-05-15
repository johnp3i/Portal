using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;

namespace Portal.Web.Data;

/// <summary>
/// Seeds a SuperAdmin user and a test Business for local development.
/// Only runs when no SuperAdmin role exists yet.
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        // Ensure the Membership database and Identity tables exist
        var membershipDb = serviceProvider.GetRequiredService<MembershipDbContext>();
        await membershipDb.Database.EnsureCreatedAsync();

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var portalDb = serviceProvider.GetRequiredService<PortalDbContext>();

        // Create SuperAdmin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("SuperAdmin"))
        {
            await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));
        }

        // Create SuperAdmin user if it doesn't exist
        const string adminEmail = "johnp@3inventors.com";
        const string adminPassword = "Onlyme_1986!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "John",
                LastName = "Papamichael",
                BusinessId = null, // SuperAdmin is not tied to a business
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
            }
        }
        else if (!await userManager.IsInRoleAsync(adminUser, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
        }

        // Create a test Business if none exists
        var hasBusinesses = await portalDb.Businesses.IgnoreQueryFilters().AnyAsync();
        if (!hasBusinesses)
        {
            var business = new Business
            {
                Name = "3 Inventors",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await portalDb.Database.ExecuteSqlRawAsync(
                @"INSERT INTO [portal].[Business] ([Name], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
                  VALUES (@p0, @p1, @p2, @p3)",
                business.Name, business.IsActive, business.CreatedAtUtc, business.UpdatedAtUtc);
        }

        // Link SuperAdmin to the first business so tenant-scoped modules work
        if (adminUser != null && !adminUser.BusinessId.HasValue)
        {
            var firstBusiness = await portalDb.Businesses.IgnoreQueryFilters().FirstOrDefaultAsync();
            if (firstBusiness != null)
            {
                adminUser.BusinessId = firstBusiness.Id;
                await userManager.UpdateAsync(adminUser);
            }
        }
    }
}
