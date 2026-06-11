using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;

namespace Portal.Web.Data;

/// <summary>
/// Seeds the Le Paris Roasting demo user for platform demonstrations.
/// Creates the user via UserManager (proper password hashing) and links
/// them to Business ID 1000 with full module permissions.
/// Only runs if the user does not already exist.
/// </summary>
public static class SeedDemoUser
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var membershipDb = serviceProvider.GetRequiredService<MembershipDbContext>();

        const string demoEmail = "demo@leparis.com";
        const string demoPassword = "Demo_2026!";
        const int businessId = 1000;

        // Ensure active subscription exists for the demo business (even if user already exists)
        var portalDb = serviceProvider.GetRequiredService<PortalDbContext>();
        await portalDb.Database.ExecuteSqlRawAsync(
            @"IF NOT EXISTS (
                SELECT 1 FROM [billing].[Subscription] WHERE [BusinessId] = {0}
              )
              BEGIN
                DECLARE @PlanId INT;
                SELECT @PlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'business';
                IF @PlanId IS NOT NULL
                BEGIN
                    INSERT INTO [billing].[Subscription]
                        ([BusinessId], [PlanId], [Status], [StripeSubscriptionId], [CurrentPeriodStart], [CurrentPeriodEnd], [CancelledAtUtc])
                    VALUES
                        ({0}, @PlanId, N'active', N'demo_sub_leparis', GETUTCDATE(), DATEADD(YEAR, 10, GETUTCDATE()), NULL);
                END
              END",
            businessId);

        // Check if user already exists
        var existingUser = await userManager.FindByEmailAsync(demoEmail);
        if (existingUser != null)
            return;

        // Create the demo user
        var demoUser = new ApplicationUser
        {
            UserName = demoEmail,
            Email = demoEmail,
            EmailConfirmed = true,
            FirstName = "Marie",
            LastName = "Dupont",
            BusinessId = businessId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(demoUser, demoPassword);
        if (!result.Succeeded)
            return;

        // Create DemoUser role if it doesn't exist
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("DemoUser"))
        {
            await roleManager.CreateAsync(new IdentityRole("DemoUser"));
        }
        await userManager.AddToRoleAsync(demoUser, "DemoUser");

        // Create UserBusiness mapping
        await membershipDb.Database.ExecuteSqlRawAsync(
            @"INSERT INTO [membership].[UserBusiness] ([UserId], [BusinessId], [IsDefault], [IsActive], [IsOwner], [CreatedAtUtc])
              VALUES ({0}, {1}, 1, 1, 1, GETUTCDATE())",
            demoUser.Id, businessId);

        // Get the UserBusinessId
        var userBusinessId = await membershipDb.Database
            .SqlQueryRaw<int>(
                @"SELECT [Id] FROM [membership].[UserBusiness] WHERE [UserId] = {0} AND [BusinessId] = {1}",
                demoUser.Id, businessId)
            .ToListAsync();

        if (userBusinessId.Count == 0)
            return;

        var ubId = userBusinessId[0];

        // Grant full permissions on all modules
        var modules = new[] { "customer", "quotation", "invoice", "revenue", "purchase", "vat", "audit" };
        foreach (var module in modules)
        {
            await membershipDb.Database.ExecuteSqlRawAsync(
                @"IF NOT EXISTS (
                    SELECT 1 FROM [membership].[UserBusinessPermission]
                    WHERE [UserBusinessId] = {0} AND [Module] = {1}
                  )
                  INSERT INTO [membership].[UserBusinessPermission] ([UserBusinessId], [Module], [AccessLevel], [IsActive], [CreatedAtUtc])
                  VALUES ({0}, {1}, N'full', 1, GETUTCDATE())",
                ubId, module);
        }

        // Create an active subscription for the demo business
        // (already handled at the top of this method)
    }
}
