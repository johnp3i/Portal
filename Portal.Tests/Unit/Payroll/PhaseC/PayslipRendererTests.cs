using Moq;
using Xunit;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Portal.Web.Services;

namespace Portal.Tests.Unit.Payroll.PhaseC;

public class PayslipRendererTests
{
    private readonly Mock<IViewRenderService> _viewRenderServiceMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly PayslipRenderer _renderer;

    public PayslipRendererTests()
    {
        _viewRenderServiceMock = new Mock<IViewRenderService>();
        _logoServiceMock = new Mock<ILogoService>();
        _renderer = new PayslipRenderer(_viewRenderServiceMock.Object, _logoServiceMock.Object);
    }

    [Fact]
    public async Task RenderPayslipHtmlAsync_UsesCorrectViewPath()
    {
        // Arrange
        var payslip = CreateSamplePayslipDetailDto();
        _viewRenderServiceMock
            .Setup(v => v.RenderViewToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync("<html>test</html>");

        // Act
        await _renderer.RenderPayslipHtmlAsync(payslip, "Test Business", "123 Street", false);

        // Assert
        _viewRenderServiceMock.Verify(v => v.RenderViewToStringAsync(
            "~/Views/Payroll/PdfTemplates/Payslip.cshtml",
            It.IsAny<PayslipPdfViewModel>()), Times.Once);
    }

    [Fact]
    public async Task RenderPayslipHtmlAsync_PopulatesModelCorrectly()
    {
        // Arrange
        var payslip = CreateSamplePayslipDetailDto();
        PayslipPdfViewModel? capturedModel = null;

        _viewRenderServiceMock
            .Setup(v => v.RenderViewToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((path, model) => capturedModel = model as PayslipPdfViewModel)
            .ReturnsAsync("<html>test</html>");

        // Act
        await _renderer.RenderPayslipHtmlAsync(payslip, "My Business", "456 Avenue", true);

        // Assert
        Assert.NotNull(capturedModel);
        Assert.Equal("My Business", capturedModel!.BusinessName);
        Assert.Equal("456 Avenue", capturedModel.BusinessAddress);
        Assert.True(capturedModel.IncludeSignature);
    }

    [Fact]
    public async Task RenderPayslipHtmlAsync_MapsPayslipDataCorrectly()
    {
        // Arrange
        var payslip = CreateSamplePayslipDetailDto();
        PayslipPdfViewModel? capturedModel = null;

        _viewRenderServiceMock
            .Setup(v => v.RenderViewToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((path, model) => capturedModel = model as PayslipPdfViewModel)
            .ReturnsAsync("<html>test</html>");

        // Act
        await _renderer.RenderPayslipHtmlAsync(payslip, "Business", "Address", false);

        // Assert
        Assert.NotNull(capturedModel);
        Assert.Same(payslip, capturedModel!.Payslip);
    }

    private static PayslipDetailDto CreateSamplePayslipDetailDto()
    {
        return new PayslipDetailDto
        {
            Id = 1,
            EmployeeName = "Test Employee",
            Year = 2027,
            Month = 7,
            TotalEarnings = 1500m,
            TotalEmployeeDeductions = 171.75m,
            NetSalary = 1328.25m,
            TotalEmployerContributions = 231m,
            EarningLines = new List<EarningLineDto>(),
            EmployeeDeductions = new List<DeductionLineDto>(),
            EmployerContributions = new List<DeductionLineDto>()
        };
    }
}
