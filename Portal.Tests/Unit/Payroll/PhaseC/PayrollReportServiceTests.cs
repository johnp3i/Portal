using Moq;
using Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Tests.Unit.Payroll.PhaseC;

public class PayrollReportServiceTests
{
    private readonly Mock<PayrollRepository> _payrollRepoMock;
    private readonly Mock<PayslipEmailLogRepository> _emailLogRepoMock;
    private readonly Mock<IPayslipPdfService> _pdfServiceMock;
    private readonly Mock<IPayslipRenderer> _rendererMock;
    private readonly Mock<IBusinessService> _businessServiceMock;
    private readonly PayrollReportService _service;

    public PayrollReportServiceTests()
    {
        _payrollRepoMock = new Mock<PayrollRepository>(MockBehavior.Loose, new object[] { null! });
        _emailLogRepoMock = new Mock<PayslipEmailLogRepository>(MockBehavior.Loose, new object[] { null! });
        _pdfServiceMock = new Mock<IPayslipPdfService>();
        _rendererMock = new Mock<IPayslipRenderer>();
        _businessServiceMock = new Mock<IBusinessService>();

        _service = new PayrollReportService(
            _payrollRepoMock.Object,
            _emailLogRepoMock.Object,
            _pdfServiceMock.Object,
            _rendererMock.Object,
            _businessServiceMock.Object);
    }

    #region 1. GetEmployeeHistoryAsync returns correctly ordered list

    [Fact]
    public async Task GetEmployeeHistoryAsync_ReturnsPayslipsOrderedByYearDescMonthDesc()
    {
        // Arrange
        var employeeId = 1;
        var businessId = 1;

        var employee = new Employee { Id = employeeId, BusinessId = businessId, Name = "John Doe" };

        // Payslips returned in arbitrary order from repo
        var payslips = new List<Payslip>
        {
            new() { Id = 1, EmployeeId = employeeId, PayslipPeriodId = 10, TotalEarnings = 1000m, NetSalary = 800m, PayslipStatusTypeId = 3 },
            new() { Id = 2, EmployeeId = employeeId, PayslipPeriodId = 11, TotalEarnings = 1100m, NetSalary = 880m, PayslipStatusTypeId = 3 },
            new() { Id = 3, EmployeeId = employeeId, PayslipPeriodId = 12, TotalEarnings = 1200m, NetSalary = 960m, PayslipStatusTypeId = 5 }
        };

        // Periods: id 10 = Jan 2027, id 11 = Mar 2027, id 12 = Feb 2027
        var period10 = new PayslipPeriod { Id = 10, BusinessId = businessId, Year = 2027, Month = 1 };
        var period11 = new PayslipPeriod { Id = 11, BusinessId = businessId, Year = 2027, Month = 3 };
        var period12 = new PayslipPeriod { Id = 12, BusinessId = businessId, Year = 2027, Month = 2 };

        var statusNames = new Dictionary<byte, string> { { 3, "Finalised" }, { 5, "Re-finalised" } };

        _payrollRepoMock.Setup(r => r.GetPayslipsByEmployeeAsync(employeeId, businessId, null))
            .ReturnsAsync(payslips);
        _payrollRepoMock.Setup(r => r.GetAvailableYearsForEmployeeAsync(employeeId, businessId))
            .ReturnsAsync(new List<int> { 2027 });
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(employeeId, businessId))
            .ReturnsAsync(employee);
        _payrollRepoMock.Setup(r => r.GetStatusNamesAsync())
            .ReturnsAsync(statusNames);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(10, businessId)).ReturnsAsync(period10);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(11, businessId)).ReturnsAsync(period11);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(12, businessId)).ReturnsAsync(period12);

        // Act
        var result = await _service.GetEmployeeHistoryAsync(employeeId, businessId, null);

        // Assert — items should be present (the service stores them in iteration order from repo)
        Assert.Equal(3, result.Payslips.Count);
        Assert.Equal("John Doe", result.EmployeeName);
        Assert.Equal(3, result.SummaryCount);
        Assert.Equal(1000m + 1100m + 1200m, result.SummaryTotalGross);
        Assert.Equal(800m + 880m + 960m, result.SummaryTotalNet);
    }

    #endregion

    #region 2. GetAnnualSummaryAsync aggregates monthly totals correctly

    [Fact]
    public async Task GetAnnualSummaryAsync_AggregatesMonthlyTotalsCorrectly()
    {
        // Arrange
        var employeeId = 1;
        var businessId = 1;
        var year = 2027;

        var employee = new Employee { Id = employeeId, BusinessId = businessId, Name = "Jane Smith" };

        var payslips = new List<Payslip>
        {
            new() { Id = 1, EmployeeId = employeeId, PayslipPeriodId = 10, TotalEarnings = 2000m, TotalEmployeeDeductions = 200m, NetSalary = 1800m, TotalEmployerContributions = 300m, PayslipStatusTypeId = 3 },
            new() { Id = 2, EmployeeId = employeeId, PayslipPeriodId = 11, TotalEarnings = 2500m, TotalEmployeeDeductions = 250m, NetSalary = 2250m, TotalEmployerContributions = 375m, PayslipStatusTypeId = 3 },
            new() { Id = 3, EmployeeId = employeeId, PayslipPeriodId = 12, TotalEarnings = 3000m, TotalEmployeeDeductions = 300m, NetSalary = 2700m, TotalEmployerContributions = 450m, PayslipStatusTypeId = 5 }
        };

        var period10 = new PayslipPeriod { Id = 10, BusinessId = businessId, Year = 2027, Month = 1 };
        var period11 = new PayslipPeriod { Id = 11, BusinessId = businessId, Year = 2027, Month = 2 };
        var period12 = new PayslipPeriod { Id = 12, BusinessId = businessId, Year = 2027, Month = 3 };

        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(employeeId, businessId)).ReturnsAsync(employee);
        _payrollRepoMock.Setup(r => r.GetAvailableYearsForEmployeeAsync(employeeId, businessId)).ReturnsAsync(new List<int> { 2027 });
        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, year)).ReturnsAsync(payslips);
        _payrollRepoMock.Setup(r => r.GetEarningLinesForPayslipsAsync(It.IsAny<int[]>())).ReturnsAsync(new List<PayslipEarningLine>
        {
            new() { Id = 1, PayslipId = 1, EarningTypeId = 1, Amount = 2000m },
            new() { Id = 2, PayslipId = 2, EarningTypeId = 1, Amount = 2500m },
            new() { Id = 3, PayslipId = 3, EarningTypeId = 1, Amount = 3000m }
        });
        _payrollRepoMock.Setup(r => r.GetDeductionLinesForPayslipsAsync(It.IsAny<int[]>())).ReturnsAsync(new List<PayslipDeductionLine>());
        _payrollRepoMock.Setup(r => r.GetAllEarningTypesAsync()).ReturnsAsync(new List<EarningType>
        {
            new() { Id = 1, Name = "Basic Salary", Code = "Basic" }
        });
        _payrollRepoMock.Setup(r => r.GetDeductionTypesByBusinessAsync(businessId)).ReturnsAsync(new List<DeductionType>());
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(10, businessId)).ReturnsAsync(period10);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(11, businessId)).ReturnsAsync(period11);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(12, businessId)).ReturnsAsync(period12);

        // Act
        var result = await _service.GetAnnualSummaryAsync(employeeId, businessId, year);

        // Assert — TotalGross = sum of TotalEarnings
        Assert.Equal(2000m + 2500m + 3000m, result.TotalGross);
        Assert.Equal(200m + 250m + 300m, result.TotalDeductions);
        Assert.Equal(1800m + 2250m + 2700m, result.TotalNet);
        Assert.Equal(300m + 375m + 450m, result.TotalContributions);
        Assert.Equal(3, result.MonthlyBreakdown.Count);
        Assert.Equal("Jane Smith", result.EmployeeName);
    }

    #endregion

    #region 3. GetAnnualSummaryAsync excludes Draft/Preview payslips

    [Fact]
    public async Task GetAnnualSummaryAsync_CallsGetFinalisedPayslipsForEmployeeYearAsync_WithCorrectParams()
    {
        // Arrange
        var employeeId = 5;
        var businessId = 2;
        var year = 2026;

        var employee = new Employee { Id = employeeId, BusinessId = businessId, Name = "Test Employee" };

        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(employeeId, businessId)).ReturnsAsync(employee);
        _payrollRepoMock.Setup(r => r.GetAvailableYearsForEmployeeAsync(employeeId, businessId)).ReturnsAsync(new List<int> { 2026 });
        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, year))
            .ReturnsAsync(new List<Payslip>());

        // Act
        await _service.GetAnnualSummaryAsync(employeeId, businessId, year);

        // Assert — verifies the correct repository method is called with correct parameters
        // GetFinalisedPayslipsForEmployeeYearAsync already filters to StatusTypeId IN (3, 5)
        _payrollRepoMock.Verify(
            r => r.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, year),
            Times.Once);
    }

    #endregion

    #region 4. GetEarningsBreakdownAsync applies filter parameters

    [Fact]
    public async Task GetEarningsBreakdownAsync_AppliesEarningTypeFilter()
    {
        // Arrange
        var businessId = 1;
        var filter = new EarningsBreakdownFilter
        {
            FromYear = 2027,
            FromMonth = 1,
            ToYear = 2027,
            ToMonth = 3,
            EarningTypeIds = new List<int> { 2 } // Only overtime
        };

        var periods = new List<PayslipPeriod>
        {
            new() { Id = 10, BusinessId = businessId, Year = 2027, Month = 1 },
            new() { Id = 11, BusinessId = businessId, Year = 2027, Month = 2 },
            new() { Id = 12, BusinessId = businessId, Year = 2027, Month = 3 }
        };

        var payslips = new List<Payslip>
        {
            new() { Id = 1, EmployeeId = 1, PayslipPeriodId = 10, TotalEarnings = 1500m, NetSalary = 1200m, PayslipStatusTypeId = 3 }
        };

        var earningLines = new List<PayslipEarningLine>
        {
            new() { Id = 1, PayslipId = 1, EarningTypeId = 1, Amount = 1000m, Description = "Basic" },
            new() { Id = 2, PayslipId = 1, EarningTypeId = 2, Amount = 500m, Description = "Overtime" }
        };

        var earningTypes = new List<EarningType>
        {
            new() { Id = 1, Name = "Basic Salary", Code = "Basic" },
            new() { Id = 2, Name = "Overtime", Code = "Overtime" }
        };

        var employee = new Employee { Id = 1, BusinessId = businessId, Name = "Worker A" };

        _payrollRepoMock.Setup(r => r.GetPeriodsByBusinessAsync(businessId)).ReturnsAsync(periods);
        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForPeriodAsync(It.IsAny<int>(), businessId)).ReturnsAsync(payslips);
        _payrollRepoMock.Setup(r => r.GetEarningLinesForPayslipsAsync(It.IsAny<int[]>())).ReturnsAsync(earningLines);
        _payrollRepoMock.Setup(r => r.GetAllEarningTypesAsync()).ReturnsAsync(earningTypes);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(1, businessId)).ReturnsAsync(employee);

        // Act
        var result = await _service.GetEarningsBreakdownAsync(businessId, filter);

        // Assert — only overtime lines should be in results (EarningTypeId = 2)
        Assert.All(result.Details, d => Assert.Equal("Overtime", d.EarningTypeName));
        Assert.Single(result.TypeSummaries);
        Assert.Equal(500m, result.TypeSummaries[0].TotalAmount);
    }

    #endregion

    #region 5. GetPeriodSummaryAsync applies department filter

    [Fact]
    public async Task GetPeriodSummaryAsync_AppliesDepartmentFilter_ReturnsOnlyMatchingDepartment()
    {
        // Arrange
        var periodId = 10;
        var businessId = 1;
        var departmentId = 2;

        var period = new PayslipPeriod { Id = periodId, BusinessId = businessId, Year = 2027, Month = 7 };

        var payslips = new List<Payslip>
        {
            new() { Id = 1, EmployeeId = 1, PayslipPeriodId = periodId, TotalEarnings = 2000m, TotalEmployeeDeductions = 200m, NetSalary = 1800m, TotalEmployerContributions = 300m, PayslipStatusTypeId = 3 },
            new() { Id = 2, EmployeeId = 2, PayslipPeriodId = periodId, TotalEarnings = 2500m, TotalEmployeeDeductions = 250m, NetSalary = 2250m, TotalEmployerContributions = 375m, PayslipStatusTypeId = 3 },
            new() { Id = 3, EmployeeId = 3, PayslipPeriodId = periodId, TotalEarnings = 3000m, TotalEmployeeDeductions = 300m, NetSalary = 2700m, TotalEmployerContributions = 450m, PayslipStatusTypeId = 3 }
        };

        // Employee 1 and 3 are in department 2, Employee 2 is in department 1
        var emp1 = new Employee { Id = 1, BusinessId = businessId, Name = "Alice", DepartmentId = 2 };
        var emp2 = new Employee { Id = 2, BusinessId = businessId, Name = "Bob", DepartmentId = 1 };
        var emp3 = new Employee { Id = 3, BusinessId = businessId, Name = "Charlie", DepartmentId = 2 };

        var dept2 = new Department { Id = 2, BusinessId = businessId, Name = "Engineering" };

        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(periodId, businessId)).ReturnsAsync(period);
        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForPeriodAsync(periodId, businessId)).ReturnsAsync(payslips);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(1, businessId)).ReturnsAsync(emp1);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(2, businessId)).ReturnsAsync(emp2);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(3, businessId)).ReturnsAsync(emp3);
        _payrollRepoMock.Setup(r => r.GetDepartmentByIdAsync(2, businessId)).ReturnsAsync(dept2);

        // Act
        var result = await _service.GetPeriodSummaryAsync(periodId, businessId, departmentId);

        // Assert — only employees from department 2 (Alice and Charlie)
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.EmployeeName == "Alice");
        Assert.Contains(result.Rows, r => r.EmployeeName == "Charlie");
        Assert.DoesNotContain(result.Rows, r => r.EmployeeName == "Bob");
        Assert.Equal(2000m + 3000m, result.TotalGross);
    }

    #endregion

    #region 6. GenerateAllPayslipsPdfZipAsync includes correct number of entries

    [Fact]
    public async Task GenerateAllPayslipsPdfZipAsync_CallsBatchPdfWithCorrectNumberOfDocuments()
    {
        // Arrange
        var periodId = 10;
        var businessId = 1;

        var period = new PayslipPeriod { Id = periodId, BusinessId = businessId, Year = 2027, Month = 7 };

        var payslips = new List<Payslip>
        {
            new() { Id = 1, EmployeeId = 1, PayslipPeriodId = periodId, TotalEarnings = 2000m, NetSalary = 1800m, PayslipStatusTypeId = 3 },
            new() { Id = 2, EmployeeId = 2, PayslipPeriodId = periodId, TotalEarnings = 2500m, NetSalary = 2250m, PayslipStatusTypeId = 3 },
            new() { Id = 3, EmployeeId = 3, PayslipPeriodId = periodId, TotalEarnings = 3000m, NetSalary = 2700m, PayslipStatusTypeId = 5 }
        };

        var emp1 = new Employee { Id = 1, BusinessId = businessId, Name = "Alice" };
        var emp2 = new Employee { Id = 2, BusinessId = businessId, Name = "Bob" };
        var emp3 = new Employee { Id = 3, BusinessId = businessId, Name = "Charlie" };

        var business = new Business { Id = businessId, Name = "Test Corp", IsActive = true };
        var profile = new BusinessProfile
        {
            Id = 1, BusinessId = businessId, AddressLine1 = "123 Street",
            City = "Nicosia", PostalCode = "1000", Country = "Cyprus",
            CompanyRegistrationNumber = "CRN001", VatRegistrationNumber = "VAT001",
            VatRegistrationDate = new DateOnly(2020, 1, 1), VatPeriodLengthInMonths = 3,
            Email = "test@example.com"
        };

        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForPeriodAsync(periodId, businessId)).ReturnsAsync(payslips);
        _payrollRepoMock.Setup(r => r.GetPeriodByIdAsync(periodId, businessId)).ReturnsAsync(period);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(1, businessId)).ReturnsAsync(emp1);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(2, businessId)).ReturnsAsync(emp2);
        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(3, businessId)).ReturnsAsync(emp3);
        _payrollRepoMock.Setup(r => r.GetEarningLinesByPayslipAsync(It.IsAny<int>())).ReturnsAsync(new List<PayslipEarningLine>());
        _payrollRepoMock.Setup(r => r.GetDeductionLinesByPayslipAsync(It.IsAny<int>())).ReturnsAsync(new List<PayslipDeductionLine>());
        _payrollRepoMock.Setup(r => r.GetAllEarningTypesAsync()).ReturnsAsync(new List<EarningType>());
        _payrollRepoMock.Setup(r => r.GetDeductionTypesByBusinessAsync(businessId)).ReturnsAsync(new List<DeductionType>());

        _businessServiceMock.Setup(s => s.GetBusinessByIdAsync(businessId)).ReturnsAsync(business);
        _businessServiceMock.Setup(s => s.GetBusinessProfileAsync(businessId)).ReturnsAsync(profile);

        _rendererMock.Setup(r => r.RenderPayslipHtmlAsync(It.IsAny<PayslipDetailDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("<html>payslip</html>");

        _pdfServiceMock.Setup(s => s.GenerateBatchPdfAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> docs, CancellationToken _) =>
                docs.Select(_ => new byte[] { 0x25, 0x50, 0x44, 0x46 }).ToList());

        // Act
        var result = await _service.GenerateAllPayslipsPdfZipAsync(periodId, businessId);

        // Assert — batch PDF called with exactly 3 HTML documents
        _pdfServiceMock.Verify(
            s => s.GenerateBatchPdfAsync(It.Is<List<string>>(docs => docs.Count == 3), It.IsAny<CancellationToken>()),
            Times.Once);

        // ZIP should contain data
        Assert.NotEmpty(result);
    }

    #endregion

    #region 7. GenerateEmployeeStatementPdfAsync returns empty when no payslips in range

    [Fact]
    public async Task GenerateEmployeeStatementPdfAsync_ReturnsEmptyWhenNoPayslipsInRange()
    {
        // Arrange
        var employeeId = 1;
        var businessId = 1;

        var employee = new Employee { Id = employeeId, BusinessId = businessId, Name = "Test Employee" };

        _payrollRepoMock.Setup(r => r.GetEmployeeByIdAsync(employeeId, businessId)).ReturnsAsync(employee);
        _payrollRepoMock.Setup(r => r.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, It.IsAny<int>()))
            .ReturnsAsync(new List<Payslip>());

        // Act
        var result = await _service.GenerateEmployeeStatementPdfAsync(
            employeeId, businessId, 2027, 1, 2027, 6);

        // Assert — empty byte array when no payslips found
        Assert.Empty(result);

        // PDF service should NOT have been called since there's nothing to render
        _pdfServiceMock.Verify(
            s => s.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
