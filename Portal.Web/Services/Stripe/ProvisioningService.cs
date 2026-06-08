using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Entities.Stripe;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models.PromoCode;
using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provisions a new tenant from a completed Stripe checkout session or promo code redemption.
/// Creates Business, UserBusiness, Subscription, StripeCustomer, Invoice, Payment,
/// and Permissions within a single database transaction.
/// </summary>
public class ProvisioningService : IProvisioningService
{
    private readonly MembershipDbContext _membershipDbContext;
    private readonly PortalDbContext _portalDbContext;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly BillingInvoiceRepository _billingInvoiceRepository;
    private readonly BillingPaymentRepository _billingPaymentRepository;
    private readonly StripeCustomerRepository _stripeCustomerRepository;
    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly PromoCodeRedemptionRepository _promoCodeRedemptionRepository;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(
        MembershipDbContext membershipDbContext,
        PortalDbContext portalDbContext,
        SubscriptionRepository subscriptionRepository,
        BillingInvoiceRepository billingInvoiceRepository,
        BillingPaymentRepository billingPaymentRepository,
        StripeCustomerRepository stripeCustomerRepository,
        PromoCodeRepository promoCodeRepository,
        PromoCodeRedemptionRepository promoCodeRedemptionRepository,
        ILogger<ProvisioningService> logger)
    {
        _membershipDbContext = membershipDbContext;
        _portalDbContext = portalDbContext;
        _subscriptionRepository = subscriptionRepository;
        _billingInvoiceRepository = billingInvoiceRepository;
        _billingPaymentRepository = billingPaymentRepository;
        _stripeCustomerRepository = stripeCustomerRepository;
        _promoCodeRepository = promoCodeRepository;
        _promoCodeRedemptionRepository = promoCodeRedemptionRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProvisioningResult> ProvisionTenantAsync(ProvisioningRequest request)
    {
        try
        {
            // Idempotency check: load PendingRegistration and verify it's not already completed
            var pendingRegistration = await _membershipDbContext.PendingRegistrations
                .Include(pr => pr.User)
                .FirstOrDefaultAsync(pr => pr.Id == request.PendingRegistrationId);

            if (pendingRegistration == null)
            {
                _logger.LogWarning(
                    "PendingRegistration not found during provisioning. PendingRegistrationId: {PendingRegistrationId}, UserId: {UserId}",
                    request.PendingRegistrationId, request.UserId);
                return new ProvisioningResult { Success = true };
            }

            if (pendingRegistration.IsCompleted)
            {
                _logger.LogInformation(
                    "PendingRegistration already completed, skipping provisioning. PendingRegistrationId: {PendingRegistrationId}, UserId: {UserId}",
                    request.PendingRegistrationId, request.UserId);
                return new ProvisioningResult { Success = true };
            }

            // Idempotency check: verify StripeSessionId hasn't already been provisioned
            var existingCustomer = await _stripeCustomerRepository.GetByStripeCustomerIdAsync(request.StripeCustomerId);
            if (existingCustomer != null)
            {
                _logger.LogInformation(
                    "StripeCustomerId already provisioned, skipping. StripeCustomerId: {StripeCustomerId}, UserId: {UserId}",
                    request.StripeCustomerId, request.UserId);
                return new ProvisioningResult { Success = true, BusinessId = existingCustomer.BusinessId };
            }

            // Get user's first and last name for business name
            var firstName = pendingRegistration.User?.FirstName ?? "User";
            var lastName = pendingRegistration.User?.LastName ?? "";
            var businessName = $"{firstName} {lastName}'s Business".Trim();

            // Begin transaction on PortalDbContext (main transaction for all portal-schema operations)
            await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                // 1. Create Business
                var businessId = await InsertBusinessAsync(businessName, now);

                // 2. Create UserBusiness in MembershipDbContext
                var userBusinessId = await InsertUserBusinessAsync(request.UserId, businessId, now);

                // 3. Create Subscription
                var subscriptionId = await _subscriptionRepository.InsertAsync(new Subscription
                {
                    BusinessId = businessId,
                    PlanId = request.PlanId,
                    Status = "active",
                    StripeSubscriptionId = request.StripeSubscriptionId,
                    CurrentPeriodStart = request.SubscriptionStart,
                    CurrentPeriodEnd = request.SubscriptionEnd,
                    CancelledAtUtc = null,
                    CreatedAtUtc = now
                });

                // 4. Create StripeCustomer mapping
                await _stripeCustomerRepository.InsertAsync(new StripeCustomer
                {
                    BusinessId = businessId,
                    StripeCustomerId = request.StripeCustomerId,
                    CreatedAtUtc = now
                });

                // 5. Create BillingInvoice
                var invoiceId = await _billingInvoiceRepository.InsertAsync(new BillingInvoice
                {
                    BusinessId = businessId,
                    StripeInvoiceId = null,
                    AmountEur = request.AmountPaid,
                    PeriodStart = request.SubscriptionStart,
                    PeriodEnd = request.SubscriptionEnd,
                    Status = "paid",
                    PaidAtUtc = now,
                    CreatedAtUtc = now
                });

                // 6. Create BillingPayment
                await _billingPaymentRepository.InsertAsync(new BillingPayment
                {
                    InvoiceId = invoiceId,
                    AmountEur = request.AmountPaid,
                    Method = "stripe",
                    PaidAtUtc = now,
                    StripePaymentIntentId = request.StripePaymentIntentId,
                    CreatedAtUtc = now
                });

                // 7. Create UserBusinessPermission for each included PlanFeature
                var planFeatures = await GetIncludedPlanFeaturesAsync(request.PlanId);
                foreach (var feature in planFeatures)
                {
                    await InsertUserBusinessPermissionAsync(userBusinessId, feature.ModuleName, AccessLevels.Full, now);
                }

                // 8. Mark PendingRegistration as completed
                pendingRegistration.IsCompleted = true;
                pendingRegistration.CompletedAtUtc = now;

                // 9. Update ApplicationUser.BusinessId so login check passes
                var updateUserQuery = @"
                    UPDATE [dbo].[AspNetUsers]
                    SET [BusinessId] = @BusinessId
                    WHERE [Id] = @UserId";

                await _membershipDbContext.Database.ExecuteSqlRawAsync(updateUserQuery,
                    new Microsoft.Data.SqlClient.SqlParameter("@BusinessId", businessId),
                    new Microsoft.Data.SqlClient.SqlParameter("@UserId", request.UserId));

                await _membershipDbContext.SaveChangesAsync();

                // Commit the transaction
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Tenant provisioned successfully. BusinessId: {BusinessId}, UserId: {UserId}, PlanId: {PlanId}, SubscriptionId: {SubscriptionId}",
                    businessId, request.UserId, request.PlanId, subscriptionId);

                return new ProvisioningResult
                {
                    Success = true,
                    BusinessId = businessId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex,
                    "Provisioning failed. UserId: {UserId}, PlanId: {PlanId}, StripeSessionId: {StripeSessionId}",
                    request.UserId, request.PlanId, request.StripeSessionId);

                return new ProvisioningResult
                {
                    Success = false,
                    ErrorMessage = "Provisioning failed due to an internal error."
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during provisioning. UserId: {UserId}, PlanId: {PlanId}, StripeSessionId: {StripeSessionId}",
                request.UserId, request.PlanId, request.StripeSessionId);

            return new ProvisioningResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred during provisioning."
            };
        }
    }

    /// <inheritdoc />
    public async Task<ProvisioningResult> ProvisionPromoTrialAsync(PromoProvisioningRequest request)
    {
        try
        {
            // Load PendingRegistration and verify it's not already completed
            var pendingRegistration = await _membershipDbContext.PendingRegistrations
                .Include(pr => pr.User)
                .FirstOrDefaultAsync(pr => pr.Id == request.PendingRegistrationId);

            if (pendingRegistration == null)
            {
                _logger.LogWarning(
                    "PendingRegistration not found during promo provisioning. PendingRegistrationId: {PendingRegistrationId}, UserId: {UserId}",
                    request.PendingRegistrationId, request.UserId);
                return new ProvisioningResult
                {
                    Success = false,
                    ErrorMessage = "Registration record not found."
                };
            }

            if (pendingRegistration.IsCompleted)
            {
                _logger.LogInformation(
                    "PendingRegistration already completed, skipping promo provisioning. PendingRegistrationId: {PendingRegistrationId}, UserId: {UserId}",
                    request.PendingRegistrationId, request.UserId);
                return new ProvisioningResult { Success = true };
            }

            // Get user's first and last name for business name
            var firstName = pendingRegistration.User?.FirstName ?? "User";
            var lastName = pendingRegistration.User?.LastName ?? "";
            var businessName = $"{firstName} {lastName}'s Business".Trim();

            // Begin transaction on PortalDbContext (main transaction for all portal-schema operations)
            await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                // 1. Create Business
                var businessId = await InsertBusinessAsync(businessName, now);

                // 2. Create UserBusiness in MembershipDbContext
                var userBusinessId = await InsertUserBusinessAsync(request.UserId, businessId, now);

                // 3. Create Subscription (trialing, null StripeSubscriptionId, period now + DurationMonths)
                var subscriptionId = await _subscriptionRepository.InsertAsync(new Subscription
                {
                    BusinessId = businessId,
                    PlanId = request.PlanId,
                    Status = "trialing",
                    StripeSubscriptionId = null,
                    CurrentPeriodStart = now,
                    CurrentPeriodEnd = now.AddMonths(request.DurationMonths),
                    CancelledAtUtc = null,
                    CreatedAtUtc = now
                });

                // 4. Create UserBusinessPermissions (all Business plan module permissions)
                var planFeatures = await GetIncludedPlanFeaturesAsync(request.PlanId);
                foreach (var feature in planFeatures)
                {
                    await InsertUserBusinessPermissionAsync(userBusinessId, feature.ModuleName, AccessLevels.Full, now);
                }

                // 5a. Re-validate promo code within transaction (guards against stale code since registration)
                var promoCode = await _promoCodeRepository.GetByIdAsync(request.PromoCodeId);
                if (promoCode == null)
                {
                    await transaction.RollbackAsync();

                    _logger.LogWarning(
                        "Promo code not found during provisioning re-validation. UserId: {UserId}, PromoCodeId: {PromoCodeId}",
                        request.UserId, request.PromoCodeId);

                    return new ProvisioningResult
                    {
                        Success = false,
                        ErrorMessage = "This promo code is no longer valid."
                    };
                }

                if (promoCode.IsRevoked)
                {
                    await transaction.RollbackAsync();

                    _logger.LogInformation(
                        "Promo code rejected during provisioning: code has been revoked. UserId: {UserId}, PromoCodeId: {PromoCodeId}",
                        request.UserId, request.PromoCodeId);

                    return new ProvisioningResult
                    {
                        Success = false,
                        ErrorMessage = "This promo code has been revoked and is no longer valid."
                    };
                }

                if (promoCode.ExpiresAtUtc <= now)
                {
                    await transaction.RollbackAsync();

                    _logger.LogInformation(
                        "Promo code rejected during provisioning: code has expired. UserId: {UserId}, PromoCodeId: {PromoCodeId}, ExpiresAtUtc: {ExpiresAtUtc}",
                        request.UserId, request.PromoCodeId, promoCode.ExpiresAtUtc);

                    return new ProvisioningResult
                    {
                        Success = false,
                        ErrorMessage = "This promo code has expired and is no longer valid."
                    };
                }

                if (promoCode.CurrentRedemptions >= promoCode.MaxRedemptions)
                {
                    await transaction.RollbackAsync();

                    _logger.LogInformation(
                        "Promo code rejected during provisioning: fully redeemed. UserId: {UserId}, PromoCodeId: {PromoCodeId}, CurrentRedemptions: {CurrentRedemptions}, MaxRedemptions: {MaxRedemptions}",
                        request.UserId, request.PromoCodeId, promoCode.CurrentRedemptions, promoCode.MaxRedemptions);

                    return new ProvisioningResult
                    {
                        Success = false,
                        ErrorMessage = "This promo code has been fully redeemed and is no longer valid."
                    };
                }

                // 5b. Increment PromoCode.CurrentRedemptions with WHERE guard (atomic concurrency check)
                var incrementSuccess = await _promoCodeRepository.IncrementRedemptionsAsync(request.PromoCodeId);
                if (!incrementSuccess)
                {
                    // Concurrent redemption race lost — rollback
                    await transaction.RollbackAsync();

                    _logger.LogInformation(
                        "Promo code redemption failed (concurrent race). UserId: {UserId}, PromoCodeId: {PromoCodeId}",
                        request.UserId, request.PromoCodeId);

                    return new ProvisioningResult
                    {
                        Success = false,
                        ErrorMessage = "This promo code is no longer valid. It may have been fully redeemed."
                    };
                }

                // 6. Create PromoCodeRedemption record
                await _promoCodeRedemptionRepository.InsertAsync(new PromoCodeRedemption
                {
                    PromoCodeId = request.PromoCodeId,
                    UserId = request.UserId,
                    BusinessId = businessId,
                    RedeemedAtUtc = now
                });

                _logger.LogInformation(
                    "Promo code redeemed. UserId={UserId}, PromoCodeId={PromoCodeId}, BusinessId={BusinessId}",
                    request.UserId, request.PromoCodeId, businessId);

                // 7. Mark PendingRegistration as completed
                pendingRegistration.IsCompleted = true;
                pendingRegistration.CompletedAtUtc = now;

                // 8. Update ApplicationUser.BusinessId so login check passes
                var updateUserQuery = @"
                    UPDATE [dbo].[AspNetUsers]
                    SET [BusinessId] = @BusinessId
                    WHERE [Id] = @UserId";

                await _membershipDbContext.Database.ExecuteSqlRawAsync(updateUserQuery,
                    new Microsoft.Data.SqlClient.SqlParameter("@BusinessId", businessId),
                    new Microsoft.Data.SqlClient.SqlParameter("@UserId", request.UserId));

                await _membershipDbContext.SaveChangesAsync();

                // Commit the transaction
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Promo trial provisioned successfully. BusinessId: {BusinessId}, UserId: {UserId}, PlanId: {PlanId}, PromoCodeId: {PromoCodeId}, DurationMonths: {DurationMonths}",
                    businessId, request.UserId, request.PlanId, request.PromoCodeId, request.DurationMonths);

                return new ProvisioningResult
                {
                    Success = true,
                    BusinessId = businessId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex,
                    "Promo provisioning failed. UserId: {UserId}, PlanId: {PlanId}, PromoCodeId: {PromoCodeId}",
                    request.UserId, request.PlanId, request.PromoCodeId);

                return new ProvisioningResult
                {
                    Success = false,
                    ErrorMessage = "Provisioning failed due to an internal error."
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during promo provisioning. UserId: {UserId}, PlanId: {PlanId}, PromoCodeId: {PromoCodeId}",
                request.UserId, request.PlanId, request.PromoCodeId);

            return new ProvisioningResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred during provisioning."
            };
        }
    }

    /// <summary>
    /// Inserts a new Business record and returns the generated Id.
    /// </summary>
    private async Task<int> InsertBusinessAsync(string name, DateTime now)
    {
        const string query = @"
            INSERT INTO [portal].[Business]
                ([Name], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @IsActive, @CreatedAtUtc, @UpdatedAtUtc)";

        var connection = _portalDbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        var transaction = _portalDbContext.Database.CurrentTransaction;
        if (transaction != null)
            command.Transaction = transaction.GetDbTransaction();

        command.Parameters.Add(new SqlParameter("@Name", name));
        command.Parameters.Add(new SqlParameter("@IsActive", true));
        command.Parameters.Add(new SqlParameter("@CreatedAtUtc", now));
        command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", now));

        var result = await command.ExecuteScalarAsync();
        return (int)result!;
    }

    /// <summary>
    /// Inserts a new UserBusiness record and returns the generated Id.
    /// </summary>
    private async Task<int> InsertUserBusinessAsync(string userId, int businessId, DateTime now)
    {
        const string query = @"
            INSERT INTO [membership].[UserBusiness]
                ([UserId], [BusinessId], [IsDefault], [IsActive], [IsOwner], [CreatedAtUtc])
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @BusinessId, @IsDefault, @IsActive, @IsOwner, @CreatedAtUtc)";

        var connection = _membershipDbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        var transaction = _membershipDbContext.Database.CurrentTransaction;
        if (transaction != null)
            command.Transaction = transaction.GetDbTransaction();

        command.Parameters.Add(new SqlParameter("@UserId", userId));
        command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
        command.Parameters.Add(new SqlParameter("@IsDefault", true));
        command.Parameters.Add(new SqlParameter("@IsActive", true));
        command.Parameters.Add(new SqlParameter("@IsOwner", true));
        command.Parameters.Add(new SqlParameter("@CreatedAtUtc", now));

        var result = await command.ExecuteScalarAsync();
        return (int)result!;
    }

    /// <summary>
    /// Gets all PlanFeature records where IsIncluded = true for the given PlanId.
    /// </summary>
    private async Task<List<PlanFeatureDto>> GetIncludedPlanFeaturesAsync(int planId)
    {
        const string query = @"
            SELECT [PlanFeature].[Id], [PlanFeature].[ModuleName]
            FROM [dbo].[PlanFeature]
            WHERE [PlanFeature].[PlanId] = @PlanId
              AND [PlanFeature].[IsIncluded] = 1";

        var results = new List<PlanFeatureDto>();
        var connection = _portalDbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        var transaction = _portalDbContext.Database.CurrentTransaction;
        if (transaction != null)
            command.Transaction = transaction.GetDbTransaction();

        command.Parameters.Add(new SqlParameter("@PlanId", planId));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PlanFeatureDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ModuleName = reader.GetString(reader.GetOrdinal("ModuleName"))
            });
        }

        return results;
    }

    /// <summary>
    /// Inserts a UserBusinessPermission record.
    /// </summary>
    private async Task InsertUserBusinessPermissionAsync(int userBusinessId, string module, string accessLevel, DateTime now)
    {
        const string query = @"
            INSERT INTO [membership].[UserBusinessPermission]
                ([UserBusinessId], [Module], [AccessLevel], [IsActive], [CreatedAtUtc])
            VALUES
                (@UserBusinessId, @Module, @AccessLevel, @IsActive, @CreatedAtUtc)";

        var connection = _membershipDbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        var transaction = _membershipDbContext.Database.CurrentTransaction;
        if (transaction != null)
            command.Transaction = transaction.GetDbTransaction();

        command.Parameters.Add(new SqlParameter("@UserBusinessId", userBusinessId));
        command.Parameters.Add(new SqlParameter("@Module", module));
        command.Parameters.Add(new SqlParameter("@AccessLevel", accessLevel));
        command.Parameters.Add(new SqlParameter("@IsActive", true));
        command.Parameters.Add(new SqlParameter("@CreatedAtUtc", now));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Internal DTO for PlanFeature query results.
    /// </summary>
    private class PlanFeatureDto
    {
        public int Id { get; set; }
        public string ModuleName { get; set; } = null!;
    }
}
