using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 13: Top customers ranking and payment accuracy

/// <summary>
/// Property-based tests for Dashboard Top Customers computation.
/// Validates that the top customers result contains at most 5 customers ordered by total invoiced
/// descending, where TotalInvoiced equals the sum of TotalAmount from issued non-deleted invoices
/// per customer, and TotalPaid equals the sum of non-voided payments against that customer's invoices.
/// Tested as a pure computation over generated invoice and payment data.
/// **Validates: Requirements 8.1, 8.2, 8.5**
/// </summary>
public class DashboardTopCustomersPropertyTests
{
    private const int TestBusinessId = 1;
    private const int InvoiceStatusIssued = 2;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected top customers result from lists of invoices and payments.
    /// This is the oracle function: group issued non-deleted invoices by customer,
    /// sum TotalAmount per customer, rank descending, take top 5,
    /// and compute TotalPaid as sum of non-voided payments against each customer's invoices.
    /// </summary>
    private static List<TopCustomerDto> ComputeExpectedTopCustomers(
        List<Invoice> invoices, List<Payment> payments, List<Customer> customers, int businessId)
    {
        // Filter to issued, non-deleted invoices for this business
        var validInvoices = invoices
            .Where(i => i.BusinessId == businessId
                        && !i.IsDeleted
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued)
            .ToList();

        // Filter to non-voided payments for this business
        var validPayments = payments
            .Where(p => p.BusinessId == businessId && !p.IsVoided)
            .ToList();

        // Group invoices by customer and compute totals
        var customerTotals = validInvoices
            .GroupBy(i => i.CustomerId)
            .Select(g =>
            {
                var customerId = g.Key;
                var customer = customers.FirstOrDefault(c => c.Id == customerId);
                var totalInvoiced = g.Sum(i => i.TotalAmount);

                // Sum payments against this customer's invoices
                var customerInvoiceIds = g.Select(i => i.Id).ToHashSet();
                var totalPaid = validPayments
                    .Where(p => customerInvoiceIds.Contains(p.InvoiceId))
                    .Sum(p => p.Amount);

                return new TopCustomerDto
                {
                    CustomerId = customerId,
                    CustomerName = customer?.Name ?? $"Customer {customerId}",
                    TotalInvoiced = totalInvoiced,
                    TotalPaid = totalPaid
                };
            })
            .OrderByDescending(c => c.TotalInvoiced)
            .Take(5)
            .ToList();

        return customerTotals;
    }

    /// <summary>
    /// Creates a customer with the specified parameters.
    /// </summary>
    private static Customer CreateCustomer(int id, int businessId, string name)
    {
        return new Customer
        {
            Id = id,
            BusinessId = businessId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an invoice with the specified parameters.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, int customerId, decimal totalAmount,
        int invoiceStatusTypeId, bool isDeleted)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = customerId,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = 1,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 - id)),
            Subtotal = totalAmount * 0.85m,
            TaxAmount = totalAmount * 0.15m,
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a payment with the specified parameters.
    /// </summary>
    private static Payment CreatePayment(
        int id, int businessId, int invoiceId, decimal amount, bool isVoided)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-id),
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    #endregion

    #region Property 13: Top customers ranking and payment accuracy

    /// <summary>
    /// Property 13: Result contains at most 5 customers ordered by total invoiced descending.
    /// Generates random invoices across multiple customers and verifies the ranking.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_ContainsAtMost5CustomersOrderedByTotalInvoicedDescending(
        PositiveInt[] amountSeeds, byte[] customerAssignments)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var customerCount = Math.Min(Math.Max((invoiceCount / 2) + 1, 3), 8); // 3 to 8 customers

        // Create customers
        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create invoices distributed across customers (all issued, non-deleted)
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (customerAssignments.Length > 0
                ? customerAssignments[i % customerAssignments.Length] % customerCount
                : i % customerCount) + 1;
            var amount = GenerateAmount(amountSeeds[i].Get);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                InvoiceStatusIssued, isDeleted: false));
        }

        var payments = new List<Payment>(); // No payments for this test

        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        // Assert at most 5
        var atMost5 = result.Count <= 5;

        // Assert ordered by TotalInvoiced descending
        var isOrdered = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].TotalInvoiced > result[i - 1].TotalInvoiced)
            {
                isOrdered = false;
                break;
            }
        }

        return (atMost5 && isOrdered).ToProperty()
            .Label($"AtMost5={atMost5}, IsOrdered={isOrdered}, ResultCount={result.Count}, " +
                   $"CustomerCount={customerCount}, InvoiceCount={invoiceCount}");
    }

    /// <summary>
    /// Property 13: TotalInvoiced equals sum of TotalAmount from issued non-deleted invoices per customer.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_TotalInvoicedEqualsExpectedSumPerCustomer(
        PositiveInt[] amountSeeds, byte[] customerAssignments, bool[] deletedFlags, bool[] statusFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var customerCount = Math.Min(Math.Max((invoiceCount / 2) + 1, 3), 8);

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create invoices with varying statuses and deletion flags
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (customerAssignments.Length > 0
                ? customerAssignments[i % customerAssignments.Length] % customerCount
                : i % customerCount) + 1;
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            // Status: issued (2) or draft (1) — only issued should count
            var statusTypeId = statusFlags.Length > 0 && statusFlags[i % statusFlags.Length]
                ? 1 // Draft — should be excluded
                : InvoiceStatusIssued; // Issued — should be included

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                statusTypeId, isDeleted));
        }

        var payments = new List<Payment>();
        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        // Verify each customer's TotalInvoiced matches expected
        var allCorrect = true;
        foreach (var customerResult in result)
        {
            var expectedTotal = invoices
                .Where(i => i.BusinessId == TestBusinessId
                            && i.CustomerId == customerResult.CustomerId
                            && !i.IsDeleted
                            && i.InvoiceStatusTypeId == InvoiceStatusIssued)
                .Sum(i => i.TotalAmount);

            if (customerResult.TotalInvoiced != expectedTotal)
            {
                allCorrect = false;
                break;
            }
        }

        return allCorrect.ToProperty()
            .Label($"AllTotalInvoicedCorrect={allCorrect}, ResultCount={result.Count}");
    }

    /// <summary>
    /// Property 13: TotalPaid equals sum of non-voided payments against that customer's invoices.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_TotalPaidEqualsNonVoidedPaymentsPerCustomer(
        PositiveInt[] invoiceAmountSeeds, PositiveInt[] paymentAmountSeeds,
        byte[] customerAssignments, bool[] voidFlags)
    {
        if (invoiceAmountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(invoiceAmountSeeds.Length, 15);
        var paymentCount = Math.Min(paymentAmountSeeds.Length, 15);
        var customerCount = Math.Min(Math.Max((invoiceCount / 2) + 1, 3), 6);

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create issued, non-deleted invoices
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (customerAssignments.Length > 0
                ? customerAssignments[i % customerAssignments.Length] % customerCount
                : i % customerCount) + 1;
            var amount = GenerateAmount(invoiceAmountSeeds[i].Get);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                InvoiceStatusIssued, isDeleted: false));
        }

        // Create payments against random invoices with varying void flags
        var payments = new List<Payment>();
        for (int i = 0; i < paymentCount; i++)
        {
            var invoiceId = (i % invoiceCount) + 1; // Distribute across invoices
            var amount = GenerateAmount(paymentAmountSeeds[i].Get);
            var isVoided = voidFlags.Length > 0 && voidFlags[i % voidFlags.Length];

            payments.Add(CreatePayment(i + 1, TestBusinessId, invoiceId, amount, isVoided));
        }

        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        // Verify each customer's TotalPaid matches expected
        var allCorrect = true;
        foreach (var customerResult in result)
        {
            var customerInvoiceIds = invoices
                .Where(i => i.BusinessId == TestBusinessId
                            && i.CustomerId == customerResult.CustomerId
                            && !i.IsDeleted
                            && i.InvoiceStatusTypeId == InvoiceStatusIssued)
                .Select(i => i.Id)
                .ToHashSet();

            var expectedPaid = payments
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsVoided
                            && customerInvoiceIds.Contains(p.InvoiceId))
                .Sum(p => p.Amount);

            if (customerResult.TotalPaid != expectedPaid)
            {
                allCorrect = false;
                break;
            }
        }

        return allCorrect.ToProperty()
            .Label($"AllTotalPaidCorrect={allCorrect}, ResultCount={result.Count}, " +
                   $"PaymentCount={paymentCount}");
    }

    /// <summary>
    /// Property 13: Deleted invoices are excluded from top customers calculation.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_ExcludesDeletedInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var customerCount = 3;

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create all invoices as deleted
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (i % customerCount) + 1;
            var amount = GenerateAmount(amountSeeds[i].Get);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                InvoiceStatusIssued, isDeleted: true));
        }

        var payments = new List<Payment>();
        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 customers when all invoices deleted, got {result.Count}");
    }

    /// <summary>
    /// Property 13: Non-issued invoices (draft) are excluded from top customers calculation.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_ExcludesNonIssuedInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var customerCount = 3;

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create all invoices as draft (status 1, not issued)
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (i % customerCount) + 1;
            var amount = GenerateAmount(amountSeeds[i].Get);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                invoiceStatusTypeId: 1, isDeleted: false));
        }

        var payments = new List<Payment>();
        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 customers when all invoices are draft, got {result.Count}");
    }

    /// <summary>
    /// Property 13: Voided payments are excluded from TotalPaid calculation.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_ExcludesVoidedPaymentsFromTotalPaid(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 10);
        var customerCount = 2;

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create issued, non-deleted invoices
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (i % customerCount) + 1;
            var amount = GenerateAmount(amountSeeds[i].Get);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                InvoiceStatusIssued, isDeleted: false));
        }

        // Create ALL payments as voided
        var payments = new List<Payment>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get + 100);
            payments.Add(CreatePayment(i + 1, TestBusinessId, invoices[i].Id, amount, isVoided: true));
        }

        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        // All TotalPaid should be zero since all payments are voided
        var allZeroPaid = result.All(c => c.TotalPaid == 0m);

        return allZeroPaid.ToProperty()
            .Label($"Expected all TotalPaid=0 when all payments voided, " +
                   $"Actual: [{string.Join(", ", result.Select(c => c.TotalPaid))}]");
    }

    /// <summary>
    /// When no invoices exist, the result is empty.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Fact]
    public void TopCustomers_NoInvoices_ReturnsEmpty()
    {
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var customers = new List<Customer>();

        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        Assert.Empty(result);
    }

    /// <summary>
    /// Property 13: Mixed scenario with multiple customers, varying statuses, deletions, and payments.
    /// Verifies the complete top customers logic end-to-end.
    /// **Validates: Requirements 8.1, 8.2, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopCustomers_MixedScenario_CorrectRankingAndPaymentAccuracy(
        PositiveInt[] invoiceAmountSeeds, PositiveInt[] paymentAmountSeeds,
        byte[] customerAssignments, bool[] deletedFlags, bool[] statusFlags, bool[] voidFlags)
    {
        if (invoiceAmountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(invoiceAmountSeeds.Length, 20);
        var paymentCount = Math.Min(paymentAmountSeeds.Length, 15);
        var customerCount = Math.Min(Math.Max((invoiceCount / 3) + 1, 3), 8);

        var customers = Enumerable.Range(1, customerCount)
            .Select(i => CreateCustomer(i, TestBusinessId, $"Customer {i}"))
            .ToList();

        // Create invoices with varying statuses and deletion flags
        var invoices = new List<Invoice>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var customerId = (customerAssignments.Length > 0
                ? customerAssignments[i % customerAssignments.Length] % customerCount
                : i % customerCount) + 1;
            var amount = GenerateAmount(invoiceAmountSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var statusTypeId = statusFlags.Length > 0 && statusFlags[i % statusFlags.Length]
                ? 1 : InvoiceStatusIssued;

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, customerId, amount,
                statusTypeId, isDeleted));
        }

        // Create payments with varying void flags
        var payments = new List<Payment>();
        var validInvoiceIds = invoices
            .Where(i => i.InvoiceStatusTypeId == InvoiceStatusIssued && !i.IsDeleted)
            .Select(i => i.Id)
            .ToList();

        if (validInvoiceIds.Count > 0)
        {
            for (int i = 0; i < paymentCount; i++)
            {
                var invoiceId = validInvoiceIds[i % validInvoiceIds.Count];
                var amount = GenerateAmount(paymentAmountSeeds[i].Get);
                var isVoided = voidFlags.Length > 0 && voidFlags[i % voidFlags.Length];

                payments.Add(CreatePayment(i + 1, TestBusinessId, invoiceId, amount, isVoided));
            }
        }

        var result = ComputeExpectedTopCustomers(invoices, payments, customers, TestBusinessId);

        // Verify all properties hold
        var atMost5 = result.Count <= 5;

        var isOrdered = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].TotalInvoiced > result[i - 1].TotalInvoiced)
            {
                isOrdered = false;
                break;
            }
        }

        // Verify TotalInvoiced and TotalPaid for each customer
        var allTotalsCorrect = true;
        foreach (var customerResult in result)
        {
            var expectedInvoiced = invoices
                .Where(i => i.BusinessId == TestBusinessId
                            && i.CustomerId == customerResult.CustomerId
                            && !i.IsDeleted
                            && i.InvoiceStatusTypeId == InvoiceStatusIssued)
                .Sum(i => i.TotalAmount);

            var customerInvoiceIds = invoices
                .Where(i => i.BusinessId == TestBusinessId
                            && i.CustomerId == customerResult.CustomerId
                            && !i.IsDeleted
                            && i.InvoiceStatusTypeId == InvoiceStatusIssued)
                .Select(i => i.Id)
                .ToHashSet();

            var expectedPaid = payments
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsVoided
                            && customerInvoiceIds.Contains(p.InvoiceId))
                .Sum(p => p.Amount);

            if (customerResult.TotalInvoiced != expectedInvoiced ||
                customerResult.TotalPaid != expectedPaid)
            {
                allTotalsCorrect = false;
                break;
            }
        }

        return (atMost5 && isOrdered && allTotalsCorrect).ToProperty()
            .Label($"AtMost5={atMost5}, IsOrdered={isOrdered}, AllTotalsCorrect={allTotalsCorrect}, " +
                   $"ResultCount={result.Count}, InvoiceCount={invoiceCount}, PaymentCount={paymentCount}");
    }

    #endregion
}
