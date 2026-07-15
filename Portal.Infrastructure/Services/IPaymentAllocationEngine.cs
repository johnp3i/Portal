using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Allocates a global payment amount across outstanding invoices.
/// Supports FIFO (oldest first) and manual (user-directed) strategies.
/// </summary>
public interface IPaymentAllocationEngine
{
    Task<AllocationResult> AllocateFifoAsync(int parentPaymentId, int customerId, decimal amount, int businessId, string userId);
    Task<AllocationResult> AllocateManualAsync(int parentPaymentId, List<ManualAllocationItem> allocations, decimal totalAmount, int businessId, string userId);
}
