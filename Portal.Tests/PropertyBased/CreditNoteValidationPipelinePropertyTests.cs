using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for Validation Pipeline Returns All Errors (Property 10).
/// For any credit note submission with multiple simultaneous violations,
/// verify ALL applicable error messages are returned in a single response.
/// **Validates: Requirements 12.10**
/// </summary>
public class CreditNoteValidationPipelinePropertyTests
{
    /// <summary>
    /// Represents a set of violation flags that can be combined to produce a DTO with multiple errors.
    /// </summary>
    private record ViolationSet(
        bool InvalidInvoiceStatus,
        bool EmptyReason,
        bool ReasonTooLong,
        bool ZeroLines,
        bool TooManyLines,
        bool HasEmptyDescription,
        bool HasQuantityZeroOrNegative,
        bool HasUnitPriceZeroOrNegative,
        bool HasVatRateOutOfRange);

    /// <summary>
    /// Generates a ViolationSet with random combinations of violations.
    /// Ensures at least one violation is present and handles mutually exclusive flags.
    /// </summary>
    private static Gen<ViolationSet> ViolationSetGen()
    {
        return from invalidInvoice in Gen.Elements(true, false)
               from emptyReason in Gen.Elements(true, false)
               from reasonTooLong in Gen.Elements(true, false)
               from zeroLines in Gen.Elements(true, false)
               from tooManyLines in Gen.Elements(true, false)
               from hasEmptyDesc in Gen.Elements(true, false)
               from hasQtyInvalid in Gen.Elements(true, false)
               from hasPriceInvalid in Gen.Elements(true, false)
               from hasVatOutOfRange in Gen.Elements(true, false)
               // Resolve mutually exclusive violations
               let resolvedReasonTooLong = emptyReason ? false : reasonTooLong
               let resolvedTooManyLines = zeroLines ? false : tooManyLines
               // Line-level violations only apply when there are lines (not zero and not too many)
               let hasNormalLines = !zeroLines && !resolvedTooManyLines
               let resolvedEmptyDesc = hasNormalLines && hasEmptyDesc
               let resolvedQtyInvalid = hasNormalLines && hasQtyInvalid
               let resolvedPriceInvalid = hasNormalLines && hasPriceInvalid
               let resolvedVatOutOfRange = hasNormalLines && hasVatOutOfRange
               let vs = new ViolationSet(
                   invalidInvoice,
                   emptyReason,
                   resolvedReasonTooLong,
                   zeroLines,
                   resolvedTooManyLines,
                   resolvedEmptyDesc,
                   resolvedQtyInvalid,
                   resolvedPriceInvalid,
                   resolvedVatOutOfRange)
               where HasAtLeastOneViolation(vs)
               select vs;
    }

    private static bool HasAtLeastOneViolation(ViolationSet vs)
    {
        return vs.InvalidInvoiceStatus || vs.EmptyReason || vs.ReasonTooLong ||
               vs.ZeroLines || vs.TooManyLines ||
               vs.HasEmptyDescription || vs.HasQuantityZeroOrNegative ||
               vs.HasUnitPriceZeroOrNegative || vs.HasVatRateOutOfRange;
    }

    /// <summary>
    /// Counts the expected minimum number of errors for a given ViolationSet.
    /// </summary>
    private static int CountExpectedErrors(ViolationSet vs)
    {
        int count = 0;

        if (vs.InvalidInvoiceStatus) count++;
        if (vs.EmptyReason) count++;
        if (vs.ReasonTooLong) count++;
        if (vs.ZeroLines) count++;
        if (vs.TooManyLines) count++;
        if (vs.HasEmptyDescription) count++;
        if (vs.HasQuantityZeroOrNegative) count++;
        if (vs.HasUnitPriceZeroOrNegative) count++;
        if (vs.HasVatRateOutOfRange) count++;

        return count;
    }

    /// <summary>
    /// Builds a CreateCreditNoteDto from a ViolationSet, introducing the specified violations.
    /// Also returns the Invoice to pass to the validation method.
    /// </summary>
    private static (CreateCreditNoteDto Dto, Invoice Invoice) BuildDtoFromViolations(ViolationSet vs)
    {
        // Build invoice
        var invoice = new Invoice
        {
            Id = 1,
            BusinessId = 1,
            InvoiceStatusTypeId = vs.InvalidInvoiceStatus ? 1 : 2, // 1 = Draft (invalid), 2 = Issued (valid)
            InvoiceFinancialStatusTypeId = 1,
            TotalAmount = 10000m
        };

        // Build reason
        string reason;
        if (vs.EmptyReason)
        {
            reason = "   "; // whitespace-only
        }
        else if (vs.ReasonTooLong)
        {
            reason = new string('X', 1001);
        }
        else
        {
            reason = "Valid reason for credit note";
        }

        // Build lines
        var lines = new List<CreateCreditNoteLineDto>();

        if (vs.ZeroLines)
        {
            // No lines at all
        }
        else if (vs.TooManyLines)
        {
            // More than 50 lines — all valid to isolate the count error
            for (int i = 0; i < 51; i++)
            {
                lines.Add(new CreateCreditNoteLineDto
                {
                    Description = $"Line {i + 1}",
                    Quantity = 1,
                    UnitPrice = 10m,
                    VatRate = 15m
                });
            }
        }
        else
        {
            // Create a single line with potential field-level violations
            var line = new CreateCreditNoteLineDto
            {
                Description = vs.HasEmptyDescription ? "  " : "Valid description",
                Quantity = vs.HasQuantityZeroOrNegative ? 0m : 5m,
                UnitPrice = vs.HasUnitPriceZeroOrNegative ? -1m : 100m,
                VatRate = vs.HasVatRateOutOfRange ? -5m : 15m
            };

            lines.Add(line);
        }

        var dto = new CreateCreditNoteDto
        {
            InvoiceId = 1,
            IssueDate = new DateOnly(2025, 6, 15),
            Reason = reason,
            VatSubmissionPeriodId = 1,
            Lines = lines
        };

        return (dto, invoice);
    }

    /// <summary>
    /// Property 10: Validation Pipeline Returns All Errors
    /// For any credit note submission with multiple simultaneous violations,
    /// verify ALL applicable error messages are returned in a single response
    /// rather than failing on the first error encountered.
    /// **Validates: Requirements 12.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidationPipeline_ReturnsAllErrors_ForMultipleViolations()
    {
        return Prop.ForAll(
            ViolationSetGen().ToArbitrary(),
            vs =>
            {
                var (dto, invoice) = BuildDtoFromViolations(vs);
                var expectedMinErrors = CountExpectedErrors(vs);

                // Use a large outstanding balance so balance check never triggers
                decimal outstandingBalance = 999_999_999m;

                var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, outstandingBalance);

                // The error list should contain at least the expected number of errors
                var hasEnoughErrors = errors.Count >= expectedMinErrors;

                return hasEnoughErrors
                    .Label($"Expected at least {expectedMinErrors} errors, got {errors.Count}. " +
                           $"Violations: InvoiceStatus={vs.InvalidInvoiceStatus}, EmptyReason={vs.EmptyReason}, " +
                           $"ReasonTooLong={vs.ReasonTooLong}, ZeroLines={vs.ZeroLines}, " +
                           $"TooManyLines={vs.TooManyLines}, EmptyDesc={vs.HasEmptyDescription}, " +
                           $"QtyInvalid={vs.HasQuantityZeroOrNegative}, PriceInvalid={vs.HasUnitPriceZeroOrNegative}, " +
                           $"VatOutOfRange={vs.HasVatRateOutOfRange}. " +
                           $"Errors: [{string.Join("; ", errors)}]");
            });
    }

    /// <summary>
    /// Property 10 (supplementary): Non-empty error list for any DTO with at least one violation.
    /// Verifies the pipeline never returns an empty list when violations exist.
    /// **Validates: Requirements 12.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidationPipeline_NeverReturnsEmpty_WhenViolationsExist()
    {
        return Prop.ForAll(
            ViolationSetGen().ToArbitrary(),
            vs =>
            {
                var (dto, invoice) = BuildDtoFromViolations(vs);

                decimal outstandingBalance = 999_999_999m;

                var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, outstandingBalance);

                return (errors.Count > 0)
                    .Label($"Expected non-empty error list but got 0 errors. " +
                           $"Violations: InvoiceStatus={vs.InvalidInvoiceStatus}, EmptyReason={vs.EmptyReason}, " +
                           $"ReasonTooLong={vs.ReasonTooLong}, ZeroLines={vs.ZeroLines}, " +
                           $"TooManyLines={vs.TooManyLines}, EmptyDesc={vs.HasEmptyDescription}, " +
                           $"QtyInvalid={vs.HasQuantityZeroOrNegative}, PriceInvalid={vs.HasUnitPriceZeroOrNegative}, " +
                           $"VatOutOfRange={vs.HasVatRateOutOfRange}");
            });
    }
}
