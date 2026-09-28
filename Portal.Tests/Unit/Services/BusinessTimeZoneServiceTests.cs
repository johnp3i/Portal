using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Notifications;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for BusinessTimeZoneService — the canonical business-local &lt;-&gt; UTC converter
/// used by the meeting timezone normalization.
/// </summary>
public class BusinessTimeZoneServiceTests
{
    // "GTB Standard Time" = Cyprus/Greece (EET/EEST): UTC+2 in winter, UTC+3 in summer (DST).
    private const string GtbWindowsId = "GTB Standard Time";

    private static PortalDbContext NewContext(out Mock<ICurrentTenantService> tenantMock)
    {
        tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(1);
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"BizTz_{Guid.NewGuid()}")
            .Options;
        return new PortalDbContext(options, tenantMock.Object);
    }

    private static BusinessTimeZoneService CreateService(PortalDbContext ctx, string defaultWindowsId = "UTC")
    {
        var options = new NotificationOptions { DefaultTimeZoneWindowsId = defaultWindowsId };
        return new BusinessTimeZoneService(ctx, options, new Mock<ILogger<BusinessTimeZoneService>>().Object);
    }

    private static void SeedBusinessWithZone(PortalDbContext ctx, int businessId, int? timeZoneId, int? tzRowId, string? windowsId)
    {
        if (tzRowId.HasValue && windowsId != null)
        {
            ctx.NotificationTimeZones.Add(new NotificationTimeZone
            {
                Id = tzRowId.Value,
                DisplayName = windowsId,
                WindowsId = windowsId,
                IanaId = "Europe/Nicosia"
            });
        }
        ctx.Businesses.Add(new Business { Id = businessId, Name = "Biz", TimeZoneId = timeZoneId });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task ConvertBusinessLocalToUtc_SummerDst_SubtractsThreeHours()
    {
        using var ctx = NewContext(out _);
        SeedBusinessWithZone(ctx, businessId: 1, timeZoneId: 5, tzRowId: 5, windowsId: GtbWindowsId);
        var svc = CreateService(ctx);

        // 2026-09-30 10:30 local (EEST, summer = UTC+3) -> 07:30 UTC
        var local = new DateTime(2026, 9, 30, 10, 30, 0);
        var utc = await svc.ConvertBusinessLocalToUtcAsync(1, local);

        Assert.Equal(new DateTime(2026, 9, 30, 7, 30, 0), utc);
    }

    [Fact]
    public async Task ConvertUtcToBusinessLocal_SummerDst_AddsThreeHours()
    {
        using var ctx = NewContext(out _);
        SeedBusinessWithZone(ctx, businessId: 1, timeZoneId: 5, tzRowId: 5, windowsId: GtbWindowsId);
        var svc = CreateService(ctx);

        var utc = new DateTime(2026, 9, 30, 7, 30, 0);
        var local = await svc.ConvertUtcToBusinessLocalAsync(1, utc);

        Assert.Equal(new DateTime(2026, 9, 30, 10, 30, 0), local);
    }

    [Fact]
    public async Task RoundTrip_LocalToUtcToLocal_IsStable()
    {
        using var ctx = NewContext(out _);
        SeedBusinessWithZone(ctx, businessId: 1, timeZoneId: 5, tzRowId: 5, windowsId: GtbWindowsId);
        var svc = CreateService(ctx);

        var local = new DateTime(2026, 1, 15, 9, 0, 0); // winter (EET, UTC+2)
        var utc = await svc.ConvertBusinessLocalToUtcAsync(1, local);
        var back = await svc.ConvertUtcToBusinessLocalAsync(1, utc);

        Assert.Equal(new DateTime(2026, 1, 15, 7, 0, 0), utc); // -2h in winter
        Assert.Equal(local, back);
    }

    [Fact]
    public async Task NullTimeZoneId_UsesDefaultWindowsId()
    {
        using var ctx = NewContext(out _);
        SeedBusinessWithZone(ctx, businessId: 1, timeZoneId: null, tzRowId: null, windowsId: null);
        var svc = CreateService(ctx, defaultWindowsId: GtbWindowsId);

        // Falls back to the default GTB zone -> summer -3h.
        var utc = await svc.ConvertBusinessLocalToUtcAsync(1, new DateTime(2026, 7, 1, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 7, 1, 9, 0, 0), utc);
    }

    [Fact]
    public async Task UnknownWindowsId_FallsBackGracefully_NoThrow()
    {
        using var ctx = NewContext(out _);
        SeedBusinessWithZone(ctx, businessId: 1, timeZoneId: 7, tzRowId: 7, windowsId: "Totally/Bogus Zone");
        var svc = CreateService(ctx, defaultWindowsId: "UTC");

        // Bogus business zone + UTC default -> treated as UTC (no shift), and must not throw.
        var utc = await svc.ConvertBusinessLocalToUtcAsync(1, new DateTime(2026, 7, 1, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 0), utc);
    }
}
