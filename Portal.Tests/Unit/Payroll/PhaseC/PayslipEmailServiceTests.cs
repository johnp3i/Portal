using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Payroll.PhaseC;

public class PayslipEmailServiceTests
{
    private readonly Mock<IPayslipPdfService> _pdfServiceMock;
    private readonly Mock<IPayslipRenderer> _rendererMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<PayslipEmailLogRepository> _emailLogRepoMock;
    private readonly Mock<PayrollRepository> _payrollRepoMock;
    private readonly Mock<IBusinessService> _businessServiceMock;
    private readonly Mock<IPayrollProgressNotifier> _progressNotifierMock;
    private readonly PayslipEmailService _service;

    public PayslipEmailServiceTests()
    {
        _pdfServiceMock = new Mock<IPayslipPdfService>();
        _rendererMock = new Mock<IPayslipRenderer>();
        _emailServiceMock = new Mock<IEmailService>();
        _emailLogRepoMock = new Mock<PayslipEmailLogRepository>(MockBehavior.Loose, new object[] { null! });
        _payrollRepoMock = new Mock<PayrollRepository>(MockBehavior.Loose, new object[] { null! });
        _businessServiceMock = new Mock<IBusinessService>();
        _progressNotifierMock = new Mock<IPayrollProgressNotifier>();

        var settings = Options.Create(new PayrollSettings
        {
            BatchEmailMaxSize = 50,
            BatchEmailDelayBetweenSendsMs = 0 // No delay in tests
        });

        _service = new PayslipEmailService(
            _pdfServiceMock.Object,
            _rendererMock.Object,
            _emailServiceMock.Object,
            _emailLogRepoMock.Object,
            _payrollRepoMock.Object,
            _businessServiceMock.Object,
            settings,
            _progressNotifierMock.Object);
    }

    #region SendPayslipAsync Tests

    [Fact]
    public async Task SendPayslipAsync_PayslipNotFound_ReturnsFailResult()
    {
        // Arrange
        _payrollRepoMock.Setup(r => r.GetPayslipDetailAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Payslip?)null);

        // Act
        var result = await _service.SendPayslipAsync(999, 1, "user-1", false);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Payslip not found.", result.Message);
    }

    [Fact]
    public async Task SendPayslipAsync_EmployeeHasNoEmail_ReturnsFailResult()
    {
        // Arrange
        var payslip = CreateTestPayslip();
        var employee = CreateTestEmployee(email: null);
        var period = CreateTestPeriod();

        SetupPayslipDetailChain(payslip, employee, period);

        // Act
        var result = await _service.SendPayslipAsync(payslip.Id, 1, "user-1", false);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Employee email address not configured.", result.Message);
    }

    [Fact]
    public async Task SendPayslipAsync_EmployeeHasEmptyEmail_ReturnsFailResult()
    {
        // Arrange
        var payslip = CreateTestPayslip();
        var employee = CreateTestEmployee(email: "   ");
        var period = CreateTestPeriod();

        SetupPayslipDetailChain(payslip, employee, period);

        // Act
        var result = await _service.SendPayslipAsync(payslip.Id, 1, "user-1", false);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Employee email address not configured.", result.Message);
    }

    [Fact]
    public async Task SendPayslipAsync_SuccessfulSend_CallsEmailServiceWithCorrectParams()
    {
        // Arrange
        var payslip = CreateTestPayslip();
        var employee = CreateTestEmployee(email: "john@example.com");
        var period = CreateTestPeriod(year: 2027, month: 7);

        SetupPayslipDetailChain(payslip, employee, period);
        SetupBusinessInfo("Test Business", "123 Main St, Nicosia");

        _rendererMock.Setup(r => r.RenderPayslipHtmlAsync(
                It.IsAny<PayslipDetailDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("<html>payslip</html>");

        _pdfServiceMock.Setup(p => p.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // Act
        var result = await _service.SendPayslipAsync(payslip.Id, 1, "user-1", false);

        // Assert
        Assert.True(result.Success);

        _emailServiceMock.Verify(e => e.SendPayslipEmailAsync(
            "john@example.com",
            "John Doe",
            "Test Business",
            "July",
            2027,
            It.Is<byte[]>(b => b.Length == 3),
            "John Doe_Payslip_July_2027.pdf"), Times.Once);
    }

    [Fact]
    public async Task SendPayslipAsync_SuccessfulSend_CreatesEmailLogWithIsSuccessTrue()
    {
        // Arrange
        var payslip = CreateTestPayslip();
        var employee = CreateTestEmployee(email: "john@example.com");
        var period = CreateTestPeriod(year: 2027, month: 7);

        SetupPayslipDetailChain(payslip, employee, period);
        SetupBusinessInfo("Test Business", "123 Main St");
        SetupRenderAndPdfGeneration();

        // Act
        var result = await _service.SendPayslipAsync(payslip.Id, 1, "user-1", false);

        // Assert
        Assert.True(result.Success);

        _emailLogRepoMock.Verify(r => r.InsertAsync(It.Is<PayslipEmailLog>(log =>
            log.PayslipId == payslip.Id &&
            log.SentByUserId == "user-1" &&
            log.SentToEmail == "john@example.com" &&
            log.IsSuccess == true)), Times.Once);
    }

    [Fact]
    public async Task SendPayslipAsync_SendFailure_CreatesEmailLogWithIsSuccessFalseAndFailureReason()
    {
        // Arrange
        var payslip = CreateTestPayslip();
        var employee = CreateTestEmployee(email: "john@example.com");
        var period = CreateTestPeriod(year: 2027, month: 7);

        SetupPayslipDetailChain(payslip, employee, period);
        SetupBusinessInfo("Test Business", "123 Main St");

        _rendererMock.Setup(r => r.RenderPayslipHtmlAsync(
                It.IsAny<PayslipDetailDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("<html>payslip</html>");

        _pdfServiceMock.Setup(p => p.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _emailServiceMock.Setup(e => e.SendPayslipEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendPayslipAsync(payslip.Id, 1, "user-1", false));

        _emailLogRepoMock.Verify(r => r.InsertAsync(It.Is<PayslipEmailLog>(log =>
            log.PayslipId == payslip.Id &&
            log.SentByUserId == "user-1" &&
            log.IsSuccess == false &&
            log.FailureReason == "SMTP connection failed")), Times.Once);
    }

    #endregion

    #region SendAllPayslipsAsync Tests

    [Fact]
    public async Task SendAllPayslipsAsync_SkipsEmployeesWithoutEmail_TracksSkippedCountCorrectly()
    {
        // Arrange
        var payslipWithEmail = new Payslip { Id = 1, EmployeeId = 10, PayslipPeriodId = 1, TotalEarnings = 1000 };
        var payslipNoEmail = new Payslip { Id = 2, EmployeeId = 20, PayslipPeriodId = 1, TotalEarnings = 1200 };

        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForPeriodAsync(1, 1))
            .ReturnsAsync(new List<Payslip> { payslipWithEmail, payslipNoEmail });

        // Employee 10 has email
        var emp10 = new Employee { Id = 10, BusinessId = 1, Name = "Alice", Email = "alice@example.com" };
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(10, 1)).ReturnsAsync(emp10);

        // Employee 20 has no email
        var emp20 = new Employee { Id = 20, BusinessId = 1, Name = "Bob", Email = null };
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(20, 1)).ReturnsAsync(emp20);

        // Setup full chain for the payslip that will be sent (payslipWithEmail)
        SetupPayslipDetailChainForId(payslipWithEmail.Id, payslipWithEmail, emp10, CreateTestPeriod());
        SetupBusinessInfo("Test Business", "123 Main St");
        SetupRenderAndPdfGeneration();

        // Act
        var result = await _service.SendAllPayslipsAsync(1, 1, "user-1", false);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("1 sent", result.Message);
        Assert.Contains("1 skipped", result.Message);
    }

    [Fact]
    public async Task SendAllPayslipsAsync_BatchContinuesAfterIndividualFailure_FailedCountIncremented()
    {
        // Arrange
        var payslip1 = new Payslip { Id = 1, EmployeeId = 10, PayslipPeriodId = 1, TotalEarnings = 1000 };
        var payslip2 = new Payslip { Id = 2, EmployeeId = 20, PayslipPeriodId = 1, TotalEarnings = 1200 };

        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForPeriodAsync(1, 1))
            .ReturnsAsync(new List<Payslip> { payslip1, payslip2 });

        var emp10 = new Employee { Id = 10, BusinessId = 1, Name = "Alice", Email = "alice@example.com" };
        var emp20 = new Employee { Id = 20, BusinessId = 1, Name = "Bob", Email = "bob@example.com" };

        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(10, 1)).ReturnsAsync(emp10);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(20, 1)).ReturnsAsync(emp20);

        var period = CreateTestPeriod();

        // Setup payslip detail chain for payslip 1 (will fail at email send)
        SetupPayslipDetailChainForId(payslip1.Id, payslip1, emp10, period);

        // Setup payslip detail chain for payslip 2 (will succeed)
        SetupPayslipDetailChainForId(payslip2.Id, payslip2, emp20, period);

        SetupBusinessInfo("Test Business", "123 Main St");

        _rendererMock.Setup(r => r.RenderPayslipHtmlAsync(
                It.IsAny<PayslipDetailDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("<html>payslip</html>");

        _pdfServiceMock.Setup(p => p.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // First call to SendPayslipEmailAsync fails, second succeeds
        var callCount = 0;
        _emailServiceMock.Setup(e => e.SendPayslipEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("SMTP error");
                return Task.CompletedTask;
            });

        // Act
        var result = await _service.SendAllPayslipsAsync(1, 1, "user-1", false);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("1 failed", result.Message);
        Assert.Contains("1 sent", result.Message);
    }

    #endregion

    #region Helper Methods

    private static Payslip CreateTestPayslip(int id = 1, int employeeId = 10, int periodId = 1)
    {
        return new Payslip
        {
            Id = id,
            EmployeeId = employeeId,
            PayslipPeriodId = periodId,
            TotalEarnings = 1500m,
            TotalEmployeeDeductions = 171.75m,
            NetSalary = 1328.25m,
            TotalEmployerContributions = 231m,
            ManagerNotes = null,
            PayslipStatusTypeId = 3
        };
    }

    private static Employee CreateTestEmployee(string? email, int id = 10, string name = "John Doe")
    {
        return new Employee
        {
            Id = id,
            BusinessId = 1,
            Name = name,
            Email = email,
            Position = "Developer",
            SocialInsuranceNumber = "SIN-123",
            IdNumber = "ID-456",
            SalaryTypeId = 1,
            BaseSalary = 1500m,
            StartDate = new DateTime(2024, 1, 1),
            IsActive = true
        };
    }

    private static PayslipPeriod CreateTestPeriod(int id = 1, int year = 2027, int month = 7)
    {
        return new PayslipPeriod
        {
            Id = id,
            BusinessId = 1,
            Year = year,
            Month = month,
            PayslipStatusTypeId = 3
        };
    }

    private void SetupPayslipDetailChain(Payslip payslip, Employee employee, PayslipPeriod period)
    {
        SetupPayslipDetailChainForId(payslip.Id, payslip, employee, period);
    }

    private void SetupPayslipDetailChainForId(int payslipId, Payslip payslip, Employee employee, PayslipPeriod period)
    {
        _payrollRepoMock.Setup(r => r.GetPayslipDetailAsync(payslipId, It.IsAny<int>()))
            .ReturnsAsync(payslip);

        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(payslip.EmployeeId, It.IsAny<int>()))
            .ReturnsAsync(employee);

        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(payslip.PayslipPeriodId, It.IsAny<int>()))
            .ReturnsAsync(period);

        _payrollRepoMock.Setup(r => r.GetEarningLinesByPayslipAsync(payslipId))
            .ReturnsAsync(new List<PayslipEarningLine>());

        _payrollRepoMock.Setup(r => r.GetDeductionLinesByPayslipAsync(payslipId))
            .ReturnsAsync(new List<PayslipDeductionLine>());

        _payrollRepoMock.Setup(r => r.GetAllEarningTypesAsync())
            .ReturnsAsync(new List<EarningType>());

        _payrollRepoMock.Setup(r => r.GetDeductionTypesByBusinessAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<DeductionType>());
    }

    private void SetupBusinessInfo(string businessName, string address)
    {
        _businessServiceMock.Setup(b => b.GetBusinessByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new Business { Id = 1, Name = businessName, IsActive = true });

        _businessServiceMock.Setup(b => b.GetBusinessProfileAsync(It.IsAny<int>()))
            .ReturnsAsync(new BusinessProfile
            {
                Id = 1,
                BusinessId = 1,
                AddressLine1 = address,
                City = "Nicosia",
                PostalCode = "1000",
                Country = "Cyprus",
                CompanyRegistrationNumber = "REG-001",
                VatRegistrationNumber = "VAT-001",
                Email = "info@test.com"
            });
    }

    private void SetupRenderAndPdfGeneration()
    {
        _rendererMock.Setup(r => r.RenderPayslipHtmlAsync(
                It.IsAny<PayslipDetailDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("<html>payslip</html>");

        _pdfServiceMock.Setup(p => p.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
    }

    #endregion
}
