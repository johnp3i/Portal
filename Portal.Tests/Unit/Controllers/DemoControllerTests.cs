using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for DemoController.Enter action.
/// Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 8.4
/// </summary>
public class DemoControllerTests
{
    private readonly Mock<IDemoInvitationService> _demoServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<ILogger<DemoController>> _loggerMock;
    private readonly DemoController _controller;

    public DemoControllerTests()
    {
        _demoServiceMock = new Mock<IDemoInvitationService>();
        _loggerMock = new Mock<ILogger<DemoController>>();

        // UserManager requires a store mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        // SignInManager requires UserManager + dependencies
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

        var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
        var roleManagerMock = new Mock<RoleManager<IdentityRole>>(
            roleStoreMock.Object, null!, null!, null!, null!);

        _controller = new DemoController(
            _demoServiceMock.Object,
            _userManagerMock.Object,
            _signInManagerMock.Object,
            roleManagerMock.Object,
            _loggerMock.Object);
    }

    #region Requirement 7.6 — Missing or empty token renders DemoInvalid

    [Fact]
    public async Task Enter_NullToken_ReturnsDemoInvalidView()
    {
        // Act
        var result = await _controller.Enter(null);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoInvalid", viewResult.ViewName);
    }

    [Fact]
    public async Task Enter_EmptyToken_ReturnsDemoInvalidView()
    {
        // Act
        var result = await _controller.Enter(string.Empty);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoInvalid", viewResult.ViewName);
    }

    [Fact]
    public async Task Enter_WhitespaceOnlyToken_ReturnsDemoInvalidView()
    {
        // Act
        var result = await _controller.Enter("   ");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoInvalid", viewResult.ViewName);
    }

    #endregion

    #region Requirement 7.3 — Invalid token renders DemoInvalid

    [Fact]
    public async Task Enter_InvalidToken_ReturnsDemoInvalidView()
    {
        // Arrange
        const string token = "nonexistent-token";
        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = false,
                ErrorReason = "invalid"
            });

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoInvalid", viewResult.ViewName);
    }

    #endregion

    #region Requirement 7.4 — Expired token renders DemoExpired

    [Fact]
    public async Task Enter_ExpiredToken_ReturnsDemoExpiredView()
    {
        // Arrange
        const string token = "expired-token-abc";
        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = false,
                ErrorReason = "expired"
            });

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoExpired", viewResult.ViewName);
    }

    #endregion

    #region Requirement 7.5 — Revoked token renders DemoRevoked

    [Fact]
    public async Task Enter_RevokedToken_ReturnsDemoRevokedView()
    {
        // Arrange
        const string token = "revoked-token-xyz";
        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = false,
                ErrorReason = "revoked"
            });

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoRevoked", viewResult.ViewName);
    }

    #endregion

    #region Requirements 7.2, 8.4 — Valid token redirects to dashboard

    [Fact]
    public async Task Enter_ValidToken_ExistingUser_RedirectsToDashboard()
    {
        // Arrange
        const string token = "valid-token-123";
        var invitation = new DemoInvitation
        {
            Id = 42,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            RecipientName = "Jane Doe",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "accessed",
            CreatedByUserId = "admin-001",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = true,
                Invitation = invitation
            });

        var existingUser = new ApplicationUser
        {
            Id = "user-demo-001",
            UserName = "prospect@example.com",
            Email = "prospect@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            BusinessId = 1000,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("prospect@example.com"))
            .ReturnsAsync(existingUser);

        _demoServiceMock
            .Setup(s => s.EnsureDemoUserBusinessAsync("user-demo-001", 1000))
            .Returns(Task.CompletedTask);

        _signInManagerMock
            .Setup(s => s.SignInWithClaimsAsync(
                existingUser,
                It.IsAny<AuthenticationProperties>(),
                It.IsAny<IEnumerable<Claim>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Home", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Enter_ValidToken_NewUser_CreatesUserAndRedirectsToDashboard()
    {
        // Arrange
        const string token = "valid-token-new-user";
        var invitation = new DemoInvitation
        {
            Id = 55,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "newprospect@example.com",
            RecipientName = "John Smith",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "sent",
            CreatedByUserId = "admin-001",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };

        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = true,
                Invitation = invitation
            });

        // User does not exist
        _userManagerMock
            .Setup(u => u.FindByEmailAsync("newprospect@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        // CreateAsync succeeds
        _userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _demoServiceMock
            .Setup(s => s.EnsureDemoUserBusinessAsync(It.IsAny<string>(), 1000))
            .Returns(Task.CompletedTask);

        _signInManagerMock
            .Setup(s => s.SignInWithClaimsAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<AuthenticationProperties>(),
                It.IsAny<IEnumerable<Claim>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Home", redirectResult.ControllerName);

        // Verify user creation was attempted
        _userManagerMock.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(user =>
                user.Email == "newprospect@example.com" &&
                user.UserName == "newprospect@example.com" &&
                user.BusinessId == 1000 &&
                user.EmailConfirmed == true &&
                user.IsActive == true),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Enter_ValidToken_UserCreationFails_ReturnsDemoInvalidView()
    {
        // Arrange
        const string token = "valid-token-create-fail";
        var invitation = new DemoInvitation
        {
            Id = 60,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "fail@example.com",
            RecipientName = null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3),
            Status = "sent",
            CreatedByUserId = "admin-001",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        _demoServiceMock
            .Setup(s => s.ValidateAndTrackAccessAsync(token))
            .ReturnsAsync(new DemoInvitationValidationResult
            {
                IsValid = true,
                Invitation = invitation
            });

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("fail@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        // CreateAsync fails
        _userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Duplicate user" }));

        // Act
        var result = await _controller.Enter(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("DemoInvalid", viewResult.ViewName);
    }

    #endregion
}
