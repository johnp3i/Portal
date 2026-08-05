using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Payroll.PhaseC;

/// <summary>
/// Unit tests for PayrollReportController.
/// Validates: Requirements 11.3, 11.4, 11.5, 7.2, 7.5, 7.12
/// </summary>
public class PayrollReportControllerTests
{
    private readonly Mock<IPayrollReportService> _reportServiceMock;
    private readonly Mock<IPayslipEmailService> _emailServiceMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly PayrollReportController _controller;

    public PayrollReportControllerTests()
    {
        _reportServiceMock = new Mock<IPayrollReportService>();
        _emailServiceMock = new Mock<IPayslipEmailService>();
        _tenantServiceMock = new Mock<ICurrentTenantService>();
        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        _controller = new PayrollReportController(
            _reportServiceMock.Object,
            _emailServiceMock.Object,
            _tenantServiceMock.Object);
    }

    private void SetupUser(bool isOwner = false, bool isSuperAdmin = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
        };
        if (isOwner) claims.Add(new Claim("IsOwner", "true"));
        if (isSuperAdmin) claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #region AxPostSendPayslipEmail — authorisation checks

    [Fact]
    public async Task AxPostSendPayslipEmail_NonOwnerNonSuperAdmin_ReturnsUnauthorisedJson()
    {
        // Arrange
        SetupUser(isOwner: false, isSuperAdmin: false);

        // Act
        var result = await _controller.AxPostSendPayslipEmail(payslipId: 1);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.False((bool)data["success"]);
        Assert.Contains("owner", ((string)data["message"]).ToLower());
    }

    [Fact]
    public async Task AxPostSendPayslipEmail_Owner_DoesNotReturnAuthorisationError()
    {
        // Arrange
        SetupUser(isOwner: true);
        _reportServiceMock.Setup(s => s.GetLastEmailForPayslipAsync(1))
            .ReturnsAsync((PayslipEmailLogDto?)null);
        _emailServiceMock.Setup(s => s.SendPayslipAsync(1, 1, "test-user-id", false))
            .ReturnsAsync(ServiceResult.Ok());

        // Act
        var result = await _controller.AxPostSendPayslipEmail(payslipId: 1);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.True((bool)data["success"]);
    }

    [Fact]
    public async Task AxPostSendPayslipEmail_SuperAdmin_DoesNotReturnAuthorisationError()
    {
        // Arrange
        SetupUser(isSuperAdmin: true);
        _reportServiceMock.Setup(s => s.GetLastEmailForPayslipAsync(1))
            .ReturnsAsync((PayslipEmailLogDto?)null);
        _emailServiceMock.Setup(s => s.SendPayslipAsync(1, 1, "test-user-id", false))
            .ReturnsAsync(ServiceResult.Ok());

        // Act
        var result = await _controller.AxPostSendPayslipEmail(payslipId: 1);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.True((bool)data["success"]);
    }

    #endregion

    #region AxPostSendPayslipEmail — duplicate detection

    [Fact]
    public async Task AxPostSendPayslipEmail_AlreadySentWithoutConfirmResend_ReturnsAlreadySentJson()
    {
        // Arrange
        SetupUser(isOwner: true);
        var sentDate = new DateTime(2027, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        _reportServiceMock.Setup(s => s.GetLastEmailForPayslipAsync(1))
            .ReturnsAsync(new PayslipEmailLogDto
            {
                Id = 1,
                PayslipId = 1,
                SentByUserName = "Admin",
                SentToEmail = "employee@example.com",
                SentAtUtc = sentDate,
                IsSuccess = true
            });

        // Act
        var result = await _controller.AxPostSendPayslipEmail(payslipId: 1, confirmResend: false);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.False((bool)data["success"]);
        Assert.True((bool)data["alreadySent"]);
        Assert.NotNull(data["sentDate"]);
    }

    [Fact]
    public async Task AxPostSendPayslipEmail_AlreadySentWithConfirmResend_CallsServiceAndReturnsSuccess()
    {
        // Arrange
        SetupUser(isOwner: true);
        _emailServiceMock.Setup(s => s.SendPayslipAsync(1, 1, "test-user-id", false))
            .ReturnsAsync(ServiceResult.Ok());

        // Act — confirmResend=true bypasses duplicate check
        var result = await _controller.AxPostSendPayslipEmail(payslipId: 1, confirmResend: true);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.True((bool)data["success"]);
        _emailServiceMock.Verify(s => s.SendPayslipAsync(1, 1, "test-user-id", false), Times.Once);
    }

    #endregion

    #region AxPostSendAllPayslipEmails — Owner access and service call

    [Fact]
    public async Task AxPostSendAllPayslipEmails_NonOwnerNonSuperAdmin_ReturnsUnauthorisedJson()
    {
        // Arrange
        SetupUser(isOwner: false, isSuperAdmin: false);

        // Act
        var result = await _controller.AxPostSendAllPayslipEmails(periodId: 5);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.False((bool)data["success"]);
        Assert.Contains("owner", ((string)data["message"]).ToLower());
    }

    [Fact]
    public async Task AxPostSendAllPayslipEmails_Owner_CallsServiceAndReturnsSummary()
    {
        // Arrange
        SetupUser(isOwner: true);
        _emailServiceMock.Setup(s => s.SendAllPayslipsAsync(5, 1, "test-user-id", false))
            .ReturnsAsync(new ServiceResult { Success = true, Message = "3 sent, 0 failed, 1 skipped" });

        // Act
        var result = await _controller.AxPostSendAllPayslipEmails(periodId: 5);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.True((bool)data["success"]);
        Assert.Contains("sent", (string)data["message"]);
        _emailServiceMock.Verify(s => s.SendAllPayslipsAsync(5, 1, "test-user-id", false), Times.Once);
    }

    #endregion

    #region AxGetDownloadPayslipPdf — service method call verification

    [Fact]
    public async Task AxGetDownloadAllPayslipsPdf_CallsServiceWithCorrectBusinessId()
    {
        // Arrange
        SetupUser(isOwner: true);
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes
        _reportServiceMock.Setup(s => s.GenerateAllPayslipsPdfZipAsync(5, 1))
            .ReturnsAsync(zipBytes);

        // Act
        var result = await _controller.AxGetDownloadAllPayslipsPdf(periodId: 5);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/zip", fileResult.ContentType);
        Assert.Equal(zipBytes, fileResult.FileContents);
        _reportServiceMock.Verify(s => s.GenerateAllPayslipsPdfZipAsync(5, 1), Times.Once);
    }

    [Fact]
    public async Task AxGetDownloadAllPayslipsPdf_EmptyResult_ReturnsJsonError()
    {
        // Arrange
        SetupUser(isOwner: true);
        _reportServiceMock.Setup(s => s.GenerateAllPayslipsPdfZipAsync(5, 1))
            .ReturnsAsync(Array.Empty<byte>());

        // Act
        var result = await _controller.AxGetDownloadAllPayslipsPdf(periodId: 5);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousObjectProperties(jsonResult.Value!);
        Assert.False((bool)data["success"]);
    }

    #endregion

    #region Page actions — correct parameters passed to service

    [Fact]
    public async Task EmployeeHistory_PassesCorrectParametersToService()
    {
        // Arrange
        SetupUser(isOwner: true);
        var expected = new EmployeePayslipHistoryDto
        {
            EmployeeId = 42,
            EmployeeName = "John Doe",
            AvailableYears = new List<int> { 2026, 2027 },
            Payslips = new List<PayslipHistoryItemDto>()
        };
        _reportServiceMock.Setup(s => s.GetEmployeeHistoryAsync(42, 1, 2027))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.EmployeeHistory(employeeId: 42, year: 2027);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(expected, viewResult.Model);
        _reportServiceMock.Verify(s => s.GetEmployeeHistoryAsync(42, 1, 2027), Times.Once);
    }

    [Fact]
    public async Task AnnualSummary_PassesCorrectParametersToService()
    {
        // Arrange
        SetupUser(isOwner: true);
        var expected = new AnnualSummaryDto
        {
            EmployeeId = 10,
            EmployeeName = "Jane Smith",
            Year = 2026,
            AvailableYears = new List<int> { 2025, 2026 }
        };
        _reportServiceMock.Setup(s => s.GetAnnualSummaryAsync(10, 1, 2026))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.AnnualSummary(employeeId: 10, year: 2026);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(expected, viewResult.Model);
        _reportServiceMock.Verify(s => s.GetAnnualSummaryAsync(10, 1, 2026), Times.Once);
    }

    [Fact]
    public async Task PeriodSummary_PassesCorrectParametersToService()
    {
        // Arrange
        SetupUser(isOwner: true);
        var expected = new PeriodSummaryDto
        {
            PeriodId = 3,
            Year = 2027,
            Month = 7,
            DepartmentFilter = 2,
            Rows = new List<PeriodSummaryRow>()
        };
        _reportServiceMock.Setup(s => s.GetPeriodSummaryAsync(3, 1, 2))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.PeriodSummary(periodId: 3, departmentId: 2);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(expected, viewResult.Model);
        _reportServiceMock.Verify(s => s.GetPeriodSummaryAsync(3, 1, 2), Times.Once);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Extracts anonymous object properties via reflection for assertion purposes.
    /// </summary>
    private static Dictionary<string, object> GetAnonymousObjectProperties(object obj)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.GetType().GetProperties())
        {
            dict[prop.Name] = prop.GetValue(obj)!;
        }
        return dict;
    }

    #endregion
}
