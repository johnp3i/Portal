using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Payroll;

public class PayslipCalculationEngineTests
{
    private readonly PayslipCalculationEngine _engine = new();

    // Cyprus deduction rates for reference calculations
    private static List<DeductionTypeWithHistory> CyprusDeductions => new()
    {
        new DeductionTypeWithHistory
        {
            Id = 1, Name = "Social Insurance", Code = "SI_Deduction",
            IsPercentage = true, DeductionCategoryTypeId = 1,
            RateHistories = new() { new DeductionRateHistory { Id = 1, DeductionTypeId = 1, Rate = 8.80m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 2, Name = "GESY", Code = "GESY_Deduction",
            IsPercentage = true, DeductionCategoryTypeId = 1,
            RateHistories = new() { new DeductionRateHistory { Id = 2, DeductionTypeId = 2, Rate = 2.65m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 3, Name = "Social Insurance", Code = "SI_Contribution",
            IsPercentage = true, DeductionCategoryTypeId = 2,
            RateHistories = new() { new DeductionRateHistory { Id = 3, DeductionTypeId = 3, Rate = 8.80m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 4, Name = "Redundancy Fund", Code = "Redundancy",
            IsPercentage = true, DeductionCategoryTypeId = 2,
            RateHistories = new() { new DeductionRateHistory { Id = 4, DeductionTypeId = 4, Rate = 1.20m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 5, Name = "Industrial Training", Code = "IndustrialTraining",
            IsPercentage = true, DeductionCategoryTypeId = 2,
            RateHistories = new() { new DeductionRateHistory { Id = 5, DeductionTypeId = 5, Rate = 0.50m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 6, Name = "Social Cohesion", Code = "SocialCohesion",
            IsPercentage = true, DeductionCategoryTypeId = 2,
            RateHistories = new() { new DeductionRateHistory { Id = 6, DeductionTypeId = 6, Rate = 2.00m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        },
        new DeductionTypeWithHistory
        {
            Id = 7, Name = "GESY", Code = "GESY_Contribution",
            IsPercentage = true, DeductionCategoryTypeId = 2,
            RateHistories = new() { new DeductionRateHistory { Id = 7, DeductionTypeId = 7, Rate = 2.90m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
        }
    };

    private static Employee BasicEmployee => new()
    {
        Id = 1, BusinessId = 1, Name = "Test Employee", SalaryTypeId = 1,
        BaseSalary = 1000m, HourlyRate = 10m, SocialInsuranceNumber = "123", IdNumber = "456",
        StartDate = new DateTime(2024, 1, 1), IsActive = true
    };

    [Fact]
    public void Calculate_BasicSalaryOnly_ReturnsCorrectCyprusValues()
    {
        // €1,000 basic → Employee deductions: 8.8% + 2.65% = €114.50 → Net: €885.50
        // Employer: 8.8% + 1.2% + 0.5% + 2.0% + 2.9% = €154.00
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 1, EarningTypeCode = "Basic", Amount = 1000m } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(1000m, result.TotalEarnings);
        Assert.Equal(114.50m, result.TotalEmployeeDeductions);
        Assert.Equal(885.50m, result.NetSalary);
        Assert.Equal(154.00m, result.TotalEmployerContributions);
    }

    [Fact]
    public void Calculate_OvertimeWithDefaultMultiplier_ReturnsCorrectAmount()
    {
        // 10hrs × €10/hr × 1.5 = €150.00
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 2, EarningTypeCode = "Overtime", OvertimeHours = 10m, OvertimeMultiplier = null } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(150.00m, result.TotalEarnings);
        Assert.Equal(1.5m, result.EarningLines[0].OvertimeMultiplier);
    }

    [Fact]
    public void Calculate_OvertimeMaxMultiplier_ReturnsCorrectAmount()
    {
        // 8hrs × €12/hr × 4.0 = €384.00
        var employee = BasicEmployee;
        employee.HourlyRate = 12m;

        var input = new PayslipCalculationInput
        {
            Employee = employee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 2, EarningTypeCode = "Overtime", OvertimeHours = 8m, OvertimeMultiplier = 4.0m } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(384.00m, result.TotalEarnings);
    }

    [Fact]
    public void Calculate_OvertimeExplicitMultiplier_UsesExplicitValue()
    {
        // 5hrs × €10/hr × 2.0 = €100.00
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 2, EarningTypeCode = "Overtime", OvertimeHours = 5m, OvertimeMultiplier = 2.0m } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(100.00m, result.TotalEarnings);
    }

    [Fact]
    public void Calculate_MultipleEarningLines_DeductionsAppliedOnTotalGross()
    {
        // Basic €600 + Holiday €150 = €750 total
        // Employee deductions on €750: 8.8% = €66, 2.65% = €19.88 → Total = €85.88
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new()
            {
                new EarningLineInput { EarningTypeId = 1, EarningTypeCode = "Basic", Amount = 600m },
                new EarningLineInput { EarningTypeId = 4, EarningTypeCode = "PaidHolidays", Amount = 150m, Description = "Christmas bonus" }
            },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(750m, result.TotalEarnings);
        // 750 × 8.8% = 66.00, 750 × 2.65% = 19.88 → Total employee deductions = 85.88
        Assert.Equal(85.88m, result.TotalEmployeeDeductions);
        Assert.Equal(664.12m, result.NetSalary);
    }

    [Fact]
    public void Calculate_MissingRate_ReturnsValidationError()
    {
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 1, EarningTypeCode = "Basic", Amount = 1000m } },
            ApplicableDeductions = new()
            {
                new DeductionTypeWithHistory
                {
                    Id = 1, Name = "Social Insurance", Code = "SI", IsPercentage = true, DeductionCategoryTypeId = 1,
                    RateHistories = new() // Empty — no rate for any date
                }
            },
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.False(result.IsValid);
        Assert.Contains("No effective rate", result.ValidationError);
    }

    [Fact]
    public void Calculate_MultiplierOutOfRange_ReturnsValidationError()
    {
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 2, EarningTypeCode = "Overtime", OvertimeHours = 5m, OvertimeMultiplier = 5.0m } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.False(result.IsValid);
        Assert.Contains("between 1.0 and 4.0", result.ValidationError);
    }

    [Fact]
    public void Calculate_MissingHourlyRateForOvertime_ReturnsValidationError()
    {
        var employee = BasicEmployee;
        employee.HourlyRate = null;

        var input = new PayslipCalculationInput
        {
            Employee = employee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 2, EarningTypeCode = "Overtime", OvertimeHours = 5m } },
            ApplicableDeductions = CyprusDeductions,
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Hourly rate", result.ValidationError);
    }

    [Fact]
    public void Calculate_PerLineRounding_AppliesCorrectly()
    {
        // €750 × 2.65% = 19.875 → should round to 19.88 (AwayFromZero)
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 1, EarningTypeCode = "Basic", Amount = 750m } },
            ApplicableDeductions = new()
            {
                new DeductionTypeWithHistory
                {
                    Id = 2, Name = "GESY", Code = "GESY_Deduction", IsPercentage = true, DeductionCategoryTypeId = 1,
                    RateHistories = new() { new DeductionRateHistory { Id = 2, DeductionTypeId = 2, Rate = 2.65m, EffectiveFromUtc = new DateTime(2024, 1, 1) } }
                }
            },
            PeriodDate = new DateTime(2027, 7, 1)
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        Assert.Equal(19.88m, result.DeductionLines[0].CalculatedAmount);
    }

    [Fact]
    public void Calculate_HistoricalRate_UsesCorrectRateForPeriod()
    {
        // Rate was 8.3% before 2024, then changed to 8.8% from 2024-01-01
        var input = new PayslipCalculationInput
        {
            Employee = BasicEmployee,
            EarningLines = new() { new EarningLineInput { EarningTypeId = 1, EarningTypeCode = "Basic", Amount = 1000m } },
            ApplicableDeductions = new()
            {
                new DeductionTypeWithHistory
                {
                    Id = 1, Name = "Social Insurance", Code = "SI", IsPercentage = true, DeductionCategoryTypeId = 1,
                    RateHistories = new()
                    {
                        new DeductionRateHistory { Id = 1, DeductionTypeId = 1, Rate = 8.30m, EffectiveFromUtc = new DateTime(2019, 1, 1), EffectiveToUtc = new DateTime(2024, 1, 1) },
                        new DeductionRateHistory { Id = 2, DeductionTypeId = 1, Rate = 8.80m, EffectiveFromUtc = new DateTime(2024, 1, 1) }
                    }
                }
            },
            PeriodDate = new DateTime(2023, 6, 1) // Before the rate change
        };

        var result = _engine.Calculate(input);

        Assert.True(result.IsValid);
        // Should use 8.3% (historical rate for 2023)
        Assert.Equal(83.00m, result.DeductionLines[0].CalculatedAmount);
    }
}
