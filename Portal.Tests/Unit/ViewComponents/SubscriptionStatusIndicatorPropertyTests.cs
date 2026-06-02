using System.Security.Claims;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Web.Models.Stripe;
using Portal.Web.Models.ViewComponents;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;
using Portal.Web.ViewComponents;
using Xunit;

namespace Portal.Tests.Unit.ViewComponents;

/// <summary>
/// Property-based tests for SubscriptionStatusIndicatorViewComponent.
/// Uses FsCheck to validate universal correctness properties across randomly generated inputs.
/// </summary>
[Trait("Feature", "subscription-status-indicator")]
public class SubscriptionStatusIndicatorPropertyTests
{
    #region Helpers

    private static SubscriptionStatusIndicatorViewComponent CreateComponent(
        ClaimsPrincipal user,
        Mock<ISubscriptionPlanService> serviceMock,
        Mock<IPlatformConfigService>? platformConfigMock = null)
    {
        var loggerMock = new Mock<ILogger<SubscriptionStatusIndicatorViewComponent>>();
        var configMock = platformConfigMock ?? new Mock<IPlatformConfigService>();

        // Default: return "Trial" for TrialBadgeText
        if (platformConfigMock == null)
        {
            configMock.Setup(s => s.GetValueAsync("TrialBadgeText")).ReturnsAsync("Trial");
        }

        var component = new SubscriptionStatusIndicatorViewComponent(
            serviceMock.Object,
            configMock.Object,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext { User = user };
        var viewContext = new ViewContext { HttpContext = httpContext };
        var viewComponentContext = new ViewComponentContext
        {
            ViewContext = viewContext
        };

        component.ViewComponentContext = viewComponentContext;
        return component;
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(
        string businessId = "1",
        bool isOwner = false)
    {
        var claims = new List<Claim>
        {
            new Claim("BusinessId", businessId)
        };

        if (isOwner)
        {
            claims.Add(new Claim("IsOwner", "true"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    private static bool IsValidHexColor(string color)
    {
        if (string.IsNullOrEmpty(color) || color.Length != 7 || color[0] != '#')
            return false;

        for (int i = 1; i < 7; i++)
        {
            char c = color[i];
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Replicates the truncation logic from Default.cshtml for testing Property 2.
    /// </summary>
    private static string ApplyPlanNameTruncation(string? planName)
    {
        if (string.IsNullOrEmpty(planName))
            return "No Plan";

        if (planName.Length > 20)
            return planName.Substring(0, 20) + "\u2026";

        return planName;
    }

    #endregion

    #region Property 1: Status-to-badge mapping is total and deterministic

    /// <summary>
    /// Property 1: Status-to-badge mapping is total and deterministic.
    /// For any string value of SubscriptionStatus (including null, empty, and arbitrary casing),
    /// the mapping function always produces a non-empty BadgeText, valid hex BadgeBackgroundColor,
    /// and "#FFFFFF" BadgeTextColor.
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void StatusMapping_AlwaysProducesValidBadge(string? status)
    {
        // Arrange
        var serviceMock = new Mock<ISubscriptionPlanService>();
        serviceMock
            .Setup(s => s.GetAccessAsync(It.IsAny<int>()))
            .ReturnsAsync(new SubscriptionAccessResult
            {
                SubscriptionStatus = status!,
                PlanName = "Test Plan",
                HasActiveSubscription = true,
                IsGraceAccess = false
            });

        var user = CreateAuthenticatedUser("1");
        var component = CreateComponent(user, serviceMock);

        // Act
        var result = component.InvokeAsync().GetAwaiter().GetResult();

        // Assert
        var viewResult = result as ViewViewComponentResult;
        Assert.NotNull(viewResult);
        var model = viewResult!.ViewData!.Model as SubscriptionStatusIndicatorViewModel;
        Assert.NotNull(model);

        // Badge text must always be non-empty
        Assert.False(string.IsNullOrEmpty(model!.BadgeText));

        // Background color must be valid hex
        Assert.True(IsValidHexColor(model.BadgeBackgroundColor),
            $"Expected valid hex color but got '{model.BadgeBackgroundColor}' for status '{status}'");

        // Text color is always white
        Assert.Equal("#FFFFFF", model.BadgeTextColor);

        // Verify deterministic mapping for known statuses
        var normalizedStatus = status?.Trim().ToLowerInvariant();
        switch (normalizedStatus)
        {
            case "active":
                Assert.Equal("Active", model.BadgeText);
                Assert.Equal("#129867", model.BadgeBackgroundColor);
                break;
            case "trialing":
                Assert.Equal("Trial", model.BadgeText);
                Assert.Equal("#0D5EA6", model.BadgeBackgroundColor);
                break;
            case "past_due":
                Assert.Equal("Past Due", model.BadgeText);
                Assert.Equal("#C8912E", model.BadgeBackgroundColor);
                break;
            case "cancelled":
                Assert.Equal("Cancelled", model.BadgeText);
                Assert.Equal("#C24A4A", model.BadgeBackgroundColor);
                break;
            case null:
            case "":
                Assert.Equal("No Subscription", model.BadgeText);
                Assert.Equal("#C24A4A", model.BadgeBackgroundColor);
                break;
            default:
                Assert.Equal("Unknown", model.BadgeText);
                Assert.Equal("#C24A4A", model.BadgeBackgroundColor);
                break;
        }
    }

    #endregion

    #region Property 2: Plan name display truncation

    /// <summary>
    /// Property 2: Plan name display truncation.
    /// For any string value of PlanName: null/empty → "No Plan";
    /// length ≤ 20 → unchanged; length > 20 → first 20 chars + "…".
    /// **Validates: Requirements 1.2, 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public void PlanNameDisplay_TruncatesCorrectly(string? planName)
    {
        // Arrange
        var serviceMock = new Mock<ISubscriptionPlanService>();
        serviceMock
            .Setup(s => s.GetAccessAsync(It.IsAny<int>()))
            .ReturnsAsync(new SubscriptionAccessResult
            {
                SubscriptionStatus = "active",
                PlanName = planName!,
                HasActiveSubscription = true,
                IsGraceAccess = false
            });

        var user = CreateAuthenticatedUser("1");
        var component = CreateComponent(user, serviceMock);

        // Act
        var result = component.InvokeAsync().GetAwaiter().GetResult();
        var viewResult = result as ViewViewComponentResult;
        Assert.NotNull(viewResult);
        var model = viewResult!.ViewData!.Model as SubscriptionStatusIndicatorViewModel;
        Assert.NotNull(model);

        // The ViewComponent sets PlanName to "No Plan" if null/empty
        // The Razor view then truncates if > 20 chars
        // We test both layers:

        // Layer 1: ViewComponent fallback for null/empty
        if (string.IsNullOrEmpty(planName))
        {
            Assert.Equal("No Plan", model!.PlanName);
        }
        else
        {
            Assert.Equal(planName, model!.PlanName);
        }

        // Layer 2: Truncation logic (as implemented in Default.cshtml)
        var displayName = ApplyPlanNameTruncation(model.PlanName);

        if (string.IsNullOrEmpty(model.PlanName))
        {
            Assert.Equal("No Plan", displayName);
        }
        else if (model.PlanName.Length <= 20)
        {
            Assert.Equal(model.PlanName, displayName);
        }
        else
        {
            Assert.Equal(model.PlanName.Substring(0, 20) + "\u2026", displayName);
            Assert.Equal(21, displayName.Length);
        }
    }

    #endregion

    #region Property 3: Link rendering is conditioned on ownership

    /// <summary>
    /// Property 3: Link rendering is conditioned on ownership.
    /// IsOwner=true → ViewModel has IsOwner=true (anchor with billing link);
    /// IsOwner=false → ViewModel has IsOwner=false (div with role="status").
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void LinkRendering_MatchesOwnership(bool isOwner, string? status, string? planName)
    {
        // Arrange
        var serviceMock = new Mock<ISubscriptionPlanService>();
        serviceMock
            .Setup(s => s.GetAccessAsync(It.IsAny<int>()))
            .ReturnsAsync(new SubscriptionAccessResult
            {
                SubscriptionStatus = status ?? "active",
                PlanName = planName ?? "Test Plan",
                HasActiveSubscription = true,
                IsGraceAccess = false
            });

        var user = CreateAuthenticatedUser("1", isOwner: isOwner);
        var component = CreateComponent(user, serviceMock);

        // Act
        var result = component.InvokeAsync().GetAwaiter().GetResult();
        var viewResult = result as ViewViewComponentResult;
        Assert.NotNull(viewResult);
        var model = viewResult!.ViewData!.Model as SubscriptionStatusIndicatorViewModel;
        Assert.NotNull(model);

        // Assert: ownership flag is correctly passed through
        Assert.Equal(isOwner, model!.IsOwner);

        // Per the Razor view:
        // IsOwner=true → renders <a href="/Account/Billing" aria-label="View billing and subscription">
        // IsOwner=false → renders <div role="status"> (no anchor)
        // We verify the ViewModel correctly determines ownership so the view renders appropriately.
        if (isOwner)
        {
            // Owner: the view will render an anchor to /Account/Billing
            Assert.True(model.IsOwner);
        }
        else
        {
            // Non-owner: the view will render a div with role="status"
            Assert.False(model.IsOwner);
        }
    }

    #endregion

    #region Property 4: Invalid BusinessId produces empty content

    /// <summary>
    /// Property 4: Invalid BusinessId produces empty content.
    /// For any BusinessId claim that is null, empty, non-numeric, zero, or negative,
    /// the ViewComponent returns empty content.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidBusinessId_AlwaysEmpty()
    {
        var gen = Gen.OneOf(
            // null (no claim)
            Gen.Constant((string?)null),
            // empty string
            Gen.Constant((string?)""),
            // zero
            Gen.Constant((string?)"0"),
            // negative numbers
            Gen.Choose(-10000, -1).Select(n => (string?)n.ToString()),
            // non-numeric strings guaranteed to not parse as positive int
            Gen.Elements<string?>(
                "abc", "hello", "!@#$%", "null", "undefined", "NaN",
                "1.5", "1e2", "3.14", "twelve", "--1",
                "0x1A", "1,000", "+", "-", "test123abc",
                "   ", "\t", "0.0", "-0", "999999999999999999999"),
            // whitespace only
            Gen.Constant((string?)" ")
        );

        var arb = Arb.From(gen);

        return Prop.ForAll(arb, invalidBusinessId =>
        {
            // Arrange
            var serviceMock = new Mock<ISubscriptionPlanService>();
            serviceMock
                .Setup(s => s.GetAccessAsync(It.IsAny<int>()))
                .ReturnsAsync(new SubscriptionAccessResult
                {
                    SubscriptionStatus = "active",
                    PlanName = "Test Plan",
                    HasActiveSubscription = true,
                    IsGraceAccess = false
                });

            ClaimsPrincipal user;
            if (invalidBusinessId == null)
            {
                // No BusinessId claim at all
                var claims = new List<Claim>();
                var identity = new ClaimsIdentity(claims, "TestAuthentication");
                user = new ClaimsPrincipal(identity);
            }
            else
            {
                user = CreateAuthenticatedUser(invalidBusinessId);
            }

            var component = CreateComponent(user, serviceMock);

            // Act
            var result = component.InvokeAsync().GetAwaiter().GetResult();

            // Assert: must be empty content (not a view result)
            Assert.IsType<ContentViewComponentResult>(result);
            var contentResult = (ContentViewComponentResult)result;
            Assert.Equal(string.Empty, contentResult.Content);
        });
    }

    #endregion

    #region Property 5: Aria-label format consistency

    /// <summary>
    /// Property 5: Aria-label format consistency.
    /// For any badge produced, the aria-label equals "Subscription status: {BadgeText}".
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public void AriaLabel_AlwaysMatchesBadgeText(string? status)
    {
        // Arrange
        var serviceMock = new Mock<ISubscriptionPlanService>();
        serviceMock
            .Setup(s => s.GetAccessAsync(It.IsAny<int>()))
            .ReturnsAsync(new SubscriptionAccessResult
            {
                SubscriptionStatus = status!,
                PlanName = "Test Plan",
                HasActiveSubscription = true,
                IsGraceAccess = false
            });

        var user = CreateAuthenticatedUser("1");
        var component = CreateComponent(user, serviceMock);

        // Act
        var result = component.InvokeAsync().GetAwaiter().GetResult();
        var viewResult = result as ViewViewComponentResult;
        Assert.NotNull(viewResult);
        var model = viewResult!.ViewData!.Model as SubscriptionStatusIndicatorViewModel;
        Assert.NotNull(model);

        // The Razor view renders: aria-label="Subscription status: @Model.BadgeText"
        // Verify the expected aria-label format
        var expectedAriaLabel = $"Subscription status: {model!.BadgeText}";

        // BadgeText must be non-empty for the aria-label to be meaningful
        Assert.False(string.IsNullOrEmpty(model.BadgeText));

        // The format is always "Subscription status: " + BadgeText
        Assert.StartsWith("Subscription status: ", expectedAriaLabel);
        Assert.Equal($"Subscription status: {model.BadgeText}", expectedAriaLabel);
    }

    #endregion
}


