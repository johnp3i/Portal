using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for FinancialStatusEngine.ComputeOutstandingBalance.
/// Tests Property 5 from the revenue-control design document.
/// </summary>
public class OutstandingBalancePropertyTests
{
    private readonly FinancialStatusEngine _engine;

    public OutstandingBalancePropertyTests()
    {
        // ComputeOutstandingBalance is a pure function — no dependencies needed
        _engine = new FinancialStatusEngine(null!, null!);
    }

    // Feature: revenue-control, Property 5: Outstanding balance computation correctness
    // **Validates: Requirements 2.1**
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_Equals_TotalAmount_Minus_Sum_Of_NonVoided_Payments()
    {
        var totalAmountGen = Gen.Choose(1, 99999999)
            .Select(i => Math.Round((decimal)i / 100m, 2));

        var paymentGen = Arb.Generate<bool>().SelectMany(isVoided =>
            Gen.Choose(1, 9999999).Select(i => new Payment
            {
                Id = 1,
                BusinessId = 1,
                InvoiceId = 1,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = Math.Round((decimal)i / 100m, 2),
                IsVoided = isVoided
            }));

        var paymentsGen = Gen.ListOf(paymentGen);

        return Prop.ForAll(
            totalAmountGen.ToArbitrary(),
            paymentsGen.ToArbitrary(),
            (totalAmount, payments) =>
            {
                var paymentList = payments.ToList();

                // Compute expected: totalAmount - sum of non-voided payment amounts
                var expectedNonVoidedSum = paymentList
                    .Where(p => !p.IsVoided)
                    .Sum(p => p.Amount);
                var expectedBalance = totalAmount - expectedNonVoidedSum;

                // Call the engine
                var actualBalance = _engine.ComputeOutstandingBalance(totalAmount, paymentList);

                return (actualBalance == expectedBalance)
                    .Label($"Expected {expectedBalance} but got {actualBalance} " +
                           $"(TotalAmount={totalAmount}, NonVoidedSum={expectedNonVoidedSum}, " +
                           $"TotalPayments={paymentList.Count}, VoidedCount={paymentList.Count(p => p.IsVoided)})");
            });
    }
}
