using Moq;
using Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Tests.Unit.Payroll.PhaseA;

/// <summary>
/// Regression tests for ConfirmBatchGenerationAsync deduction-line persistence.
///
/// Bug: deduction lines were persisted by resolving the DeductionType via its display Name.
/// Employee and employer Social Insurance / GESY share the same Name ("Social Insurance",
/// "GESY"), so a name lookup collided and stamped the employer contribution lines with the
/// employee deduction type Id (and category 1). On the payslip they then rendered under
/// "Employee Deductions". The fix resolves by DeductionTypeId instead.
/// </summary>
public class PayrollServiceConfirmBatchTests
{
    private const int BusinessId = 1;
    private const int PeriodId = 100;
    private const int EmployeeId = 500;

    // Business 1 deduction types — employee (cat 1) and employer (cat 2) SI/GESY share names.
    private const int SiDeductionId = 8;     // "Social Insurance" employee
    private const int GesyDeductionId = 9;   // "GESY" employee
    private const int SiContributionId = 10; // "Social Insurance" employer
    private const int RedundancyId = 11;
    private const int IndTrainingId = 12;
    private const int SocCohesionId = 13;
    private const int GesyContributionId = 14; // "GESY" employer

    private static DeductionType Ded(int id, string name, string code, byte category) => new()
    {
        Id = id, Name = name, Code = code, IsPercentage = true,
        DeductionCategoryTypeId = category, BusinessId = BusinessId, IsActive = true,
        Country = "CY", IsTemplate = false
    };

    private static List<DeductionType> BusinessDeductions() => new()
    {
        Ded(SiDeductionId, "Social Insurance", "SI_Deduction", 1),
        Ded(GesyDeductionId, "GESY", "GESY_Deduction", 1),
        Ded(SiContributionId, "Social Insurance", "SI_Contribution", 2),
        Ded(RedundancyId, "Redundancy Fund", "Redundancy", 2),
        Ded(IndTrainingId, "Industrial Training Fund", "IndustrialTraining", 2),
        Ded(SocCohesionId, "Social Cohesion Fund", "SocialCohesion", 2),
        Ded(GesyContributionId, "GESY", "GESY_Contribution", 2)
    };

    private static DeductionRateHistory Rate(int id, int typeId, decimal rate) => new()
    {
        Id = id, DeductionTypeId = typeId, Rate = rate,
        EffectiveFromUtc = new DateTime(2024, 1, 1), EffectiveToUtc = null
    };

    private static List<DeductionRateHistory> RatesFor(int typeId)
    {
        var rate = typeId switch
        {
            SiDeductionId => 8.80m,
            GesyDeductionId => 2.65m,
            SiContributionId => 8.80m,
            RedundancyId => 1.20m,
            IndTrainingId => 0.50m,
            SocCohesionId => 2.00m,
            GesyContributionId => 2.90m,
            _ => 0m
        };
        // rate-history Id offset so each type has a distinct rate-history row
        return new List<DeductionRateHistory> { Rate(1000 + typeId, typeId, rate) };
    }

    [Fact]
    public async Task ConfirmBatchGeneration_PersistsEmployerContributionsWithEmployerTypeIds_NotEmployeeTwins()
    {
        // Arrange
        var repo = new Mock<PayrollRepository>(MockBehavior.Loose, new object[] { null! });

        var period = new PayslipPeriod { Id = PeriodId, BusinessId = BusinessId, Year = 2026, Month = 7, PayslipStatusTypeId = 1 };
        var employee = new Employee
        {
            Id = EmployeeId, BusinessId = BusinessId, Name = "Test Employee",
            SalaryTypeId = 1, BaseSalary = 1200m, HourlyRate = null, IsActive = true,
            IsPayeApplicable = false
        };
        var basicEarningType = new EarningType { Id = 1, Name = "Basic Salary", Code = "Basic", IsActive = true, SortOrder = 1 };

        repo.Setup(r => r.GetPeriodByIdAsync(PeriodId, BusinessId)).ReturnsAsync(period);
        repo.Setup(r => r.GetAllEarningTypesAsync()).ReturnsAsync(new List<EarningType> { basicEarningType });
        repo.Setup(r => r.GetActiveDeductionsWithRatesAsync(BusinessId)).ReturnsAsync(BusinessDeductions());
        repo.Setup(r => r.GetActiveEmployeesForPeriodAsync(BusinessId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Employee> { employee });
        repo.Setup(r => r.GetDepartmentsByBusinessAsync(BusinessId)).ReturnsAsync(new List<Department>());
        repo.Setup(r => r.GetDefaultEarningsByEmployeeAsync(EmployeeId)).ReturnsAsync(new List<EmployeeDefaultEarnings>());
        repo.Setup(r => r.GetRateHistoryAsync(It.IsAny<int>())).ReturnsAsync((int id) => RatesFor(id));
        repo.Setup(r => r.InsertPayslipAsync(It.IsAny<Payslip>())).ReturnsAsync(9999);
        repo.Setup(r => r.InsertEarningLineAsync(It.IsAny<PayslipEarningLine>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdatePeriodStatusAsync(PeriodId, (byte)2, (byte)1, null)).ReturnsAsync(true);
        repo.Setup(r => r.UpdateAllPayslipStatusesInPeriodAsync(PeriodId, (byte)2)).Returns(Task.CompletedTask);

        // Capture every persisted deduction line
        var persistedLines = new List<PayslipDeductionLine>();
        repo.Setup(r => r.InsertDeductionLineAsync(It.IsAny<PayslipDeductionLine>()))
            .Callback<PayslipDeductionLine>(l => persistedLines.Add(l))
            .Returns(Task.CompletedTask);

        // Real engine (pure) + real orchestrator; PAYE path never taken (IsPayeApplicable = false).
        var engine = new PayslipCalculationEngine();
        var payeService = new Mock<IPayeCalculationService>();
        var businessService = new Mock<IBusinessService>();
        var orchestrator = new PayslipCalculationOrchestrator(engine, payeService.Object, repo.Object, businessService.Object);

        var service = new PayrollService(
            repo.Object,
            engine,
            orchestrator,
            Mock.Of<IPayslipPeriodStatusService>(),
            Mock.Of<IPayslipAuditService>(),
            Mock.Of<IPayrollPnlService>(),
            Mock.Of<IComplianceIntegrationService>(),
            Mock.Of<IPayslipPdfService>(),
            Mock.Of<IPayslipRenderer>(),
            businessService.Object,
            null!);

        // Act
        var result = await service.ConfirmBatchGenerationAsync(PeriodId, BusinessId);

        // Assert — operation succeeded
        Assert.True(result.Success);

        // All 7 types must be persisted exactly once each, with no employee twin reused for an employer line.
        Assert.Equal(7, persistedLines.Count);
        var distinctTypeIds = persistedLines.Select(l => l.DeductionTypeId).Distinct().ToList();
        Assert.Equal(7, distinctTypeIds.Count);

        // The two formerly-colliding employer lines must point at the EMPLOYER type ids (10, 14),
        // not the employee twins (8, 9), and carry category 2.
        var employerSi = persistedLines.Single(l => l.DeductionTypeId == SiContributionId);
        Assert.Equal((byte)2, employerSi.DeductionCategoryTypeId);
        Assert.Equal(8.80m, employerSi.Rate);

        var employerGesy = persistedLines.Single(l => l.DeductionTypeId == GesyContributionId);
        Assert.Equal((byte)2, employerGesy.DeductionCategoryTypeId);
        Assert.Equal(2.90m, employerGesy.Rate);

        // The employee twins must appear exactly once each, as category-1 lines.
        var employeeSi = persistedLines.Single(l => l.DeductionTypeId == SiDeductionId);
        Assert.Equal((byte)1, employeeSi.DeductionCategoryTypeId);
        Assert.Equal(8.80m, employeeSi.Rate);

        var employeeGesy = persistedLines.Single(l => l.DeductionTypeId == GesyDeductionId);
        Assert.Equal((byte)1, employeeGesy.DeductionCategoryTypeId);
        Assert.Equal(2.65m, employeeGesy.Rate);

        // Exactly 2 employee-category lines and 5 employer-category lines.
        Assert.Equal(2, persistedLines.Count(l => l.DeductionCategoryTypeId == 1));
        Assert.Equal(5, persistedLines.Count(l => l.DeductionCategoryTypeId == 2));

        // Each persisted line must carry the rate-history id for its OWN type (not a twin's).
        var employerSiExpectedRateHistId = 1000 + SiContributionId;
        Assert.Equal(employerSiExpectedRateHistId, employerSi.DeductionRateHistoryId);
        var employerGesyExpectedRateHistId = 1000 + GesyContributionId;
        Assert.Equal(employerGesyExpectedRateHistId, employerGesy.DeductionRateHistoryId);
    }
}
