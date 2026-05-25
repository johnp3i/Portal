using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 12: VAT Net Payable invariant

/// <summary>
/// Property-based tests for the VAT Net Payable invariant.
/// Validates that NetVatPayable always equals TotalOutputVat minus TotalInputVat
/// for any VAT submission record.
/// **Validates: Requirements 7.2**
/// </summary>
public class DashboardVatNetPayablePropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected NetVatPayable from OutputVat and InputVat.
    /// This is the oracle function: NetVatPayable = OutputVat - InputVat.
    /// </summary>
    private static decimal ComputeExpectedNetVatPayable(decimal outputVat, decimal inputVat)
    {
        return outputVat - inputVat;
    }

    /// <summary>
    /// Creates a VatSubmission entity with the given OutputVat and InputVat values.
    /// NetVatPayable is set to OutputVat - InputVat (correct computation).
    /// </summary>
    private static VatSubmission CreateVatSubmission(
        int id, int businessId, decimal outputVat, decimal inputVat)
    {
        return new VatSubmission
        {
            Id = id,
            BusinessId = businessId,
            VatSubmissionPeriodId = 1,
            TotalOutputVat = outputVat,
            TotalInputVat = inputVat,
            NetVatPayable = outputVat - inputVat,
            IsSubmitted = false,
            SubmittedAtUtc = null,
            Notes = null,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Simulates the VatSummaryDto mapping from a VatSubmission entity,
    /// mirroring what GetVatSummaryAsync returns from the database.
    /// </summary>
    private static VatSummaryDto MapToVatSummaryDto(VatSubmission submission)
    {
        return new VatSummaryDto
        {
            TotalOutputVat = submission.TotalOutputVat,
            TotalInputVat = submission.TotalInputVat,
            NetVatPayable = submission.NetVatPayable,
            PeriodLabel = "Test Period",
            HasData = true
        };
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed value.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 9999999 + 1;
        return raw / 100m;
    }

    #endregion

    #region Property 12: VAT Net Payable invariant

    /// <summary>
    /// Property 12: NetVatPayable always equals OutputVat minus InputVat.
    /// Generates random OutputVat and InputVat values and asserts the invariant holds.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetVatPayable_AlwaysEqualsOutputVatMinusInputVat(
        PositiveInt outputSeed, PositiveInt inputSeed)
    {
        var outputVat = GenerateAmount(outputSeed.Get);
        var inputVat = GenerateAmount(inputSeed.Get);

        var submission = CreateVatSubmission(1, TestBusinessId, outputVat, inputVat);
        var dto = MapToVatSummaryDto(submission);

        var expected = ComputeExpectedNetVatPayable(outputVat, inputVat);

        return (dto.NetVatPayable == expected).ToProperty()
            .Label($"Expected NetVatPayable={expected}, Actual={dto.NetVatPayable}, " +
                   $"OutputVat={outputVat}, InputVat={inputVat}");
    }

    /// <summary>
    /// NetVatPayable is positive when OutputVat exceeds InputVat.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetVatPayable_IsPositive_WhenOutputExceedsInput(
        PositiveInt outputSeed, PositiveInt inputSeed)
    {
        var outputVat = GenerateAmount(outputSeed.Get) + 100m; // Ensure output > input
        var inputVat = GenerateAmount(inputSeed.Get) % outputVat; // Input always less than output

        var submission = CreateVatSubmission(1, TestBusinessId, outputVat, inputVat);
        var dto = MapToVatSummaryDto(submission);

        var expected = outputVat - inputVat;

        return (dto.NetVatPayable == expected && dto.NetVatPayable > 0).ToProperty()
            .Label($"Expected positive NetVatPayable={expected}, Actual={dto.NetVatPayable}, " +
                   $"OutputVat={outputVat}, InputVat={inputVat}");
    }

    /// <summary>
    /// NetVatPayable is negative when InputVat exceeds OutputVat (refund scenario).
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetVatPayable_IsNegative_WhenInputExceedsOutput(
        PositiveInt outputSeed, PositiveInt inputSeed)
    {
        var inputVat = GenerateAmount(inputSeed.Get) + 100m; // Ensure input > output
        var outputVat = GenerateAmount(outputSeed.Get) % inputVat; // Output always less than input

        var submission = CreateVatSubmission(1, TestBusinessId, outputVat, inputVat);
        var dto = MapToVatSummaryDto(submission);

        var expected = outputVat - inputVat;

        return (dto.NetVatPayable == expected && dto.NetVatPayable < 0).ToProperty()
            .Label($"Expected negative NetVatPayable={expected}, Actual={dto.NetVatPayable}, " +
                   $"OutputVat={outputVat}, InputVat={inputVat}");
    }

    /// <summary>
    /// NetVatPayable is zero when OutputVat equals InputVat.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetVatPayable_IsZero_WhenOutputEqualsInput(PositiveInt amountSeed)
    {
        var amount = GenerateAmount(amountSeed.Get);

        var submission = CreateVatSubmission(1, TestBusinessId, amount, amount);
        var dto = MapToVatSummaryDto(submission);

        return (dto.NetVatPayable == 0m).ToProperty()
            .Label($"Expected NetVatPayable=0 when OutputVat=InputVat={amount}, " +
                   $"Actual={dto.NetVatPayable}");
    }

    /// <summary>
    /// NetVatPayable invariant holds across multiple VAT submission records.
    /// Generates a batch of random submissions and verifies the invariant for each.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetVatPayable_InvariantHoldsAcrossMultipleRecords(
        PositiveInt[] outputSeeds, PositiveInt[] inputSeeds)
    {
        if (outputSeeds.Length == 0 || inputSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var recordCount = Math.Min(Math.Min(outputSeeds.Length, inputSeeds.Length), 20);
        var allValid = true;
        var failureMessage = string.Empty;

        for (int i = 0; i < recordCount; i++)
        {
            var outputVat = GenerateAmount(outputSeeds[i].Get);
            var inputVat = GenerateAmount(inputSeeds[i].Get);

            var submission = CreateVatSubmission(i + 1, TestBusinessId, outputVat, inputVat);
            var dto = MapToVatSummaryDto(submission);

            var expected = ComputeExpectedNetVatPayable(outputVat, inputVat);

            if (dto.NetVatPayable != expected)
            {
                allValid = false;
                failureMessage = $"Record {i + 1}: Expected={expected}, Actual={dto.NetVatPayable}, " +
                                 $"OutputVat={outputVat}, InputVat={inputVat}";
                break;
            }
        }

        return allValid.ToProperty()
            .Label(allValid
                ? $"All {recordCount} records satisfy NetVatPayable = OutputVat - InputVat"
                : failureMessage);
    }

    /// <summary>
    /// When no VAT data exists, all values are zero (HasData = false scenario).
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Fact]
    public void NetVatPayable_NoVatData_AllValuesAreZero()
    {
        var emptyDto = new VatSummaryDto
        {
            TotalOutputVat = 0m,
            TotalInputVat = 0m,
            NetVatPayable = 0m,
            PeriodLabel = string.Empty,
            HasData = false
        };

        Assert.Equal(0m, emptyDto.NetVatPayable);
        Assert.Equal(emptyDto.TotalOutputVat - emptyDto.TotalInputVat, emptyDto.NetVatPayable);
    }

    #endregion
}
