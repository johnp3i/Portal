using System.Security.Cryptography;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Orchestrates proposal sharing: builds the render model, generates the HTML snapshot,
/// creates a secure share token, persists the share record, and sends the notification email.
/// </summary>
public class ProposalService : IProposalService
{
    private readonly ProposalShareRepository _shareRepository;
    private readonly ProposalSectionRepository _sectionRepository;
    private readonly QuotationRepository _quotationRepository;
    private readonly QuotationLineRepository _lineRepository;
    private readonly BusinessRepository _businessRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly BusinessLogoRepository _logoRepository;
    private readonly IProposalRenderer _renderer;
    private readonly IEmailService _emailService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly QuotationContactRepository _contactRepository;
    private readonly ILogger<ProposalService> _logger;

    public ProposalService(
        ProposalShareRepository shareRepository,
        ProposalSectionRepository sectionRepository,
        QuotationRepository quotationRepository,
        QuotationLineRepository lineRepository,
        BusinessRepository businessRepository,
        CustomerRepository customerRepository,
        BusinessLogoRepository logoRepository,
        QuotationContactRepository contactRepository,
        IProposalRenderer renderer,
        IEmailService emailService,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        ILogger<ProposalService> logger)
    {
        _shareRepository = shareRepository;
        _sectionRepository = sectionRepository;
        _quotationRepository = quotationRepository;
        _lineRepository = lineRepository;
        _businessRepository = businessRepository;
        _customerRepository = customerRepository;
        _logoRepository = logoRepository;
        _contactRepository = contactRepository;
        _renderer = renderer;
        _emailService = emailService;
        _tenantService = tenantService;
        _businessService = businessService;
        _logger = logger;
    }

    public async Task<ProposalShare> ShareAsync(int quotationId, DateTimeOffset expiresAtUtc, List<int> heroLogoIds, int? metaLogoId, string userId, string? recipientEmail = null, bool sendEmail = true)
    {
        // Validate expiration date (must be at least 1 day in the future)
        if (expiresAtUtc <= DateTimeOffset.UtcNow.AddDays(1))
            throw new ArgumentException("Expiration date must be at least 1 day in the future.");

        var businessId = _tenantService.CurrentBusinessId;

        // Load quotation
        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, businessId);
        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        // Load customer
        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(quotation.CustomerId, businessId);
        if (customer == null)
            throw new InvalidOperationException("Customer not found.");

        // Use recipient email override if provided, otherwise fall back to customer email
        var emailToUse = !string.IsNullOrWhiteSpace(recipientEmail) ? recipientEmail : customer.Email;
        if (string.IsNullOrWhiteSpace(emailToUse))
            throw new ArgumentException("A recipient email is required for sharing a proposal.");

        // Load business and profile
        var business = await _businessRepository.GetByIdAsync(businessId);
        if (business == null)
            throw new InvalidOperationException("Business not found.");

        business.BusinessProfile = await _businessService.GetBusinessProfileAsync(businessId);

        // Load lines and sections
        var lines = await _lineRepository.GetByQuotationIdAsync(quotationId);
        var sections = await _sectionRepository.GetByQuotationIdAsync(quotationId);

        // Load logos
        var allLogos = await _logoRepository.GetByBusinessIdAsync(businessId);
        var heroLogos = allLogos.Where(l => heroLogoIds.Contains(l.Id)).ToList();
        var metaLogo = metaLogoId.HasValue ? allLogos.FirstOrDefault(l => l.Id == metaLogoId.Value) : null;

        // Build render model
        var contact = quotation.QuotationContactId.HasValue
            ? await _contactRepository.GetByIdAsync(quotation.QuotationContactId.Value)
            : null;
        var renderModel = BuildRenderModel(quotation, customer, business, lines, sections, heroLogos, metaLogo, contact);

        // Render HTML snapshot
        var snapshotHtml = await _renderer.RenderAsync(renderModel);

        // Generate secure token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var shareToken = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Deactivate previous shares
        await _shareRepository.DeactivateByQuotationIdAsync(quotationId);

        // Create share record
        var share = new ProposalShare
        {
            QuotationId = quotationId,
            BusinessId = businessId,
            ShareToken = shareToken,
            SnapshotHtml = snapshotHtml,
            CustomerEmail = emailToUse,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = userId,
            IsActive = true
        };

        await _shareRepository.InsertAsync(share);

        // Send email notification if requested (failure doesn't roll back the share)
        if (sendEmail)
        {
            try
            {
                await _emailService.SendProposalEmailAsync(
                    emailToUse,
                    shareToken,
                    quotation.Reference,
                    business.Name,
                    expiresAtUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send proposal email for quotation {QuotationId}", quotationId);
            }
        }

        _logger.LogInformation("Proposal shared for quotation {QuotationId} with token {Token}", quotationId, shareToken);

        return share;
    }

    public async Task<string> PreviewAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, businessId);
        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(quotation.CustomerId, businessId);
        if (customer == null)
            throw new InvalidOperationException("Customer not found.");

        var business = await _businessRepository.GetByIdAsync(businessId);
        if (business == null)
            throw new InvalidOperationException("Business not found.");

        business.BusinessProfile = await _businessService.GetBusinessProfileAsync(businessId);

        var lines = await _lineRepository.GetByQuotationIdAsync(quotationId);
        var sections = await _sectionRepository.GetByQuotationIdAsync(quotationId);

        var allLogos = await _logoRepository.GetByBusinessIdAsync(businessId);
        var heroLogos = allLogos.Where(l => heroLogoIds.Contains(l.Id)).ToList();
        var metaLogo = metaLogoId.HasValue ? allLogos.FirstOrDefault(l => l.Id == metaLogoId.Value) : null;

        var renderModel = BuildRenderModel(quotation, customer, business, lines, sections, heroLogos, metaLogo,
            quotation.QuotationContactId.HasValue ? await _contactRepository.GetByIdAsync(quotation.QuotationContactId.Value) : null);

        return await _renderer.RenderAsync(renderModel);
    }

    public async Task<ProposalShare?> GetByTokenAsync(string token)
    {
        return await _shareRepository.GetByTokenAsync(token);
    }

    public async Task<ProposalShare?> GetActiveShareByQuotationIdAsync(int quotationId)
    {
        return await _shareRepository.GetActiveByQuotationIdAsync(quotationId);
    }

    public async Task<List<ProposalShare>> GetSharesByQuotationIdAsync(int quotationId)
    {
        return await _shareRepository.GetByQuotationIdAsync(quotationId);
    }

    private static ProposalRenderModel BuildRenderModel(
        Quotation quotation,
        Customer customer,
        Business business,
        List<QuotationLine> lines,
        List<ProposalSection> sections,
        List<BusinessLogo> heroLogos,
        BusinessLogo? metaLogo,
        QuotationContact? contact = null)
    {
        var profile = business.BusinessProfile;
        var businessAddress = profile != null
            ? $"{profile.AddressLine1}, {profile.City}, {profile.PostalCode}, {profile.Country}"
            : string.Empty;

        var customerAddress = !string.IsNullOrEmpty(customer.AddressLine1)
            ? $"{customer.AddressLine1}, {customer.City}, {customer.PostalCode}, {customer.Country}"
            : null;

        // Group lines by section
        var sectionModels = new List<ProposalSectionRenderModel>();

        if (sections.Any())
        {
            foreach (var section in sections.OrderBy(s => s.SortOrder))
            {
                var sectionLines = lines
                    .Where(l => l.ProposalSectionId == section.Id)
                    .OrderBy(l => l.SortOrder)
                    .Select(MapLine)
                    .ToList();

                sectionModels.Add(new ProposalSectionRenderModel
                {
                    Name = section.Name,
                    Description = section.Description,
                    Notes = section.Notes,
                    ColumnConfiguration = section.ColumnConfiguration,
                    SortOrder = section.SortOrder,
                    SectionType = section.SectionType,
                    IsEmphasized = section.IsEmphasized,
                    AccentColor = section.AccentColor,
                    Label = section.Label,
                    IsTotalsTableShown = section.IsTotalsTableShown,
                    IsHalfWidth = section.IsHalfWidth,
                    Lines = sectionLines
                });
            }

            // Lines without a section go into a default group
            var unsectioned = lines
                .Where(l => l.ProposalSectionId == null)
                .OrderBy(l => l.SortOrder)
                .Select(MapLine)
                .ToList();

            if (unsectioned.Any())
            {
                sectionModels.Add(new ProposalSectionRenderModel
                {
                    Name = "General",
                    ColumnConfiguration = "OneTime",
                    SortOrder = int.MaxValue,
                    Lines = unsectioned
                });
            }
        }
        else
        {
            // No sections defined — all lines in one default section
            sectionModels.Add(new ProposalSectionRenderModel
            {
                Name = "Items",
                ColumnConfiguration = "OneTime",
                SortOrder = 0,
                Lines = lines.OrderBy(l => l.SortOrder).Select(MapLine).ToList()
            });
        }

        return new ProposalRenderModel
        {
            BusinessName = business.Name,
            CompanyRegistrationNumber = profile?.CompanyRegistrationNumber ?? string.Empty,
            VatRegistrationNumber = profile?.VatRegistrationNumber ?? string.Empty,
            BusinessAddress = businessAddress,
            BusinessEmail = profile?.Email ?? string.Empty,
            BusinessPhone = profile?.TelephoneNumber,
            BusinessMobile = profile?.MobileNumber,
            CurrencySymbol = profile?.CurrencySymbol ?? "€",
            CustomerName = customer.Name,
            CustomerContactPerson = customer.ContactPerson,
            CustomerEmail = customer.Email,
            CustomerAddress = customerAddress,
            Reference = quotation.Reference,
            ValidUntil = quotation.ValidUntil,
            Subtotal = quotation.Subtotal,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount,
            Notes = quotation.Notes,
            IsGrandTotalShown = quotation.IsGrandTotalShown,
            PreparedByName = contact?.Name,
            PreparedByEmail = contact?.Email,
            PreparedByPhone = contact?.TelephoneNumber,
            Sections = sectionModels,
            HeroLogos = heroLogos.Select(l => new ProposalLogoRenderModel
            {
                DisplayName = l.DisplayName,
                PublicUrl = l.PublicUrl
            }).ToList(),
            MetaLogo = metaLogo != null ? new ProposalLogoRenderModel
            {
                DisplayName = metaLogo.DisplayName,
                PublicUrl = metaLogo.PublicUrl
            } : null
        };
    }

    private static ProposalLineRenderModel MapLine(QuotationLine line) => new()
    {
        Description = line.Description,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        VatRate = line.VatRate,
        Discount = line.Discount,
        DiscountType = line.DiscountType,
        LineTotal = line.LineTotal,
        SortOrder = line.SortOrder,
        ReferenceUrl = line.ReferenceUrl,
        Subtitle = line.Subtitle
    };
}
