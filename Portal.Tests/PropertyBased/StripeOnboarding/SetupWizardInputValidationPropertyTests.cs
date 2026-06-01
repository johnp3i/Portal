using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 9: Setup wizard input validation

/// <summary>
/// Property-based tests for setup wizard input validation.
/// For any business name that is empty, whitespace-only, or exceeds 200 characters, the setup
/// wizard SHALL reject the submission without creating a BusinessProfile. For any VAT number
/// exceeding 50 characters, the submission SHALL be rejected. For any uploaded file that is not
/// PNG/JPG/SVG or exceeds 2MB, the submission SHALL be rejected.
/// **Validates: Requirements 4.7, 4.8, 4.4, 4.12**
/// </summary>
public class SetupWizardInputValidationPropertyTests
{
    private const int TestBusinessId = 1;

    /// <summary>
    /// Creates a configured SetupWizardService with an in-memory database and mocked logo service.
    /// Returns the service and the DbContext for verification.
    /// </summary>
    private static (SetupWizardService Service, PortalDbContext DbContext) CreateService()
    {
        var currentTenantService = new Mock<ICurrentTenantService>();
        currentTenantService.Setup(s => s.CurrentBusinessId).Returns(TestBusinessId);

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PortalDb_SetupValidation_{Guid.NewGuid()}")
            .Options;

        var portalDbContext = new PortalDbContext(portalOptions, currentTenantService.Object);

        // Seed the business record that the service expects to find
        portalDbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Placeholder Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        portalDbContext.SaveChanges();

        var logoService = new Mock<ILogoService>();
        var logger = new Mock<ILogger<SetupWizardService>>();

        var service = new SetupWizardService(portalDbContext, logoService.Object, logger.Object);

        return (service, portalDbContext);
    }

    /// <summary>
    /// Creates a mock IFormFile with the specified content type and size.
    /// </summary>
    private static IFormFile CreateMockFile(string contentType, long sizeBytes, string fileName = "logo.png")
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(sizeBytes);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        return fileMock.Object;
    }

    #region Property 9a: Empty or whitespace business name is rejected

    /// <summary>
    /// Property 9a: For any business name that is null, empty, or whitespace-only,
    /// the SetupWizardService SHALL reject the submission with a validation error
    /// and SHALL NOT create a BusinessProfile record.
    /// **Validates: Requirements 4.7, 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(InvalidBusinessNameArbitrary) })]
    public Property EmptyOrWhitespaceBusinessName_IsRejected(InvalidBusinessNameInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var model = new SetupWizardModel
            {
                BusinessName = input.BusinessName!,
                CurrencySymbol = "€"
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("BusinessName")
                && noProfileCreated).ToProperty()
                .Label($"BusinessName='{input.BusinessName}': Expected rejection with BusinessName error, " +
                       $"Got Success={result.Success}, HasError={result.ValidationErrors.ContainsKey("BusinessName")}, " +
                       $"ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9b: Business name exceeding 200 characters is rejected

    /// <summary>
    /// Property 9b: For any business name exceeding 200 characters, the SetupWizardService
    /// SHALL reject the submission with a validation error and SHALL NOT create a BusinessProfile.
    /// **Validates: Requirements 4.7, 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(TooLongBusinessNameArbitrary) })]
    public Property BusinessNameExceeding200Chars_IsRejected(TooLongBusinessNameInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var model = new SetupWizardModel
            {
                BusinessName = input.BusinessName,
                CurrencySymbol = "€"
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("BusinessName")
                && noProfileCreated
                && input.BusinessName.Length > 200).ToProperty()
                .Label($"BusinessName length={input.BusinessName.Length}: Expected rejection, " +
                       $"Got Success={result.Success}, ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9c: VAT number exceeding 50 characters is rejected

    /// <summary>
    /// Property 9c: For any VAT number exceeding 50 characters, the SetupWizardService
    /// SHALL reject the submission with a validation error and SHALL NOT create a BusinessProfile.
    /// **Validates: Requirements 4.4, 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(TooLongVatNumberArbitrary) })]
    public Property VatNumberExceeding50Chars_IsRejected(TooLongVatNumberInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var model = new SetupWizardModel
            {
                BusinessName = "Valid Business Name",
                CurrencySymbol = "€",
                VatNumber = input.VatNumber
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("VatNumber")
                && noProfileCreated
                && input.VatNumber.Length > 50).ToProperty()
                .Label($"VatNumber length={input.VatNumber.Length}: Expected rejection, " +
                       $"Got Success={result.Success}, ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9d: Empty or null currency symbol is rejected

    /// <summary>
    /// Property 9d: For any empty or null currency symbol, the SetupWizardService
    /// SHALL reject the submission with a validation error and SHALL NOT create a BusinessProfile.
    /// **Validates: Requirements 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(InvalidCurrencyArbitrary) })]
    public Property EmptyOrNullCurrency_IsRejected(InvalidCurrencyInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var model = new SetupWizardModel
            {
                BusinessName = "Valid Business Name",
                CurrencySymbol = input.CurrencySymbol!
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("CurrencySymbol")
                && noProfileCreated).ToProperty()
                .Label($"CurrencySymbol='{input.CurrencySymbol}': Expected rejection with CurrencySymbol error, " +
                       $"Got Success={result.Success}, HasError={result.ValidationErrors.ContainsKey("CurrencySymbol")}, " +
                       $"ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9e: Invalid logo file types are rejected

    /// <summary>
    /// Property 9e: For any uploaded file that is not PNG, JPG, or SVG content type,
    /// the SetupWizardService SHALL reject the submission with a validation error
    /// and SHALL NOT create a BusinessProfile.
    /// **Validates: Requirements 4.8, 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(InvalidLogoTypeArbitrary) })]
    public Property InvalidLogoFileType_IsRejected(InvalidLogoTypeInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var logoFile = CreateMockFile(input.ContentType, input.FileSizeBytes, input.FileName);

            var model = new SetupWizardModel
            {
                BusinessName = "Valid Business Name",
                CurrencySymbol = "€",
                Logo = logoFile
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("Logo")
                && noProfileCreated).ToProperty()
                .Label($"ContentType='{input.ContentType}': Expected rejection with Logo error, " +
                       $"Got Success={result.Success}, HasError={result.ValidationErrors.ContainsKey("Logo")}, " +
                       $"ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9f: Logo files exceeding 2MB are rejected

    /// <summary>
    /// Property 9f: For any uploaded file exceeding 2MB (2,097,152 bytes), the SetupWizardService
    /// SHALL reject the submission with a validation error and SHALL NOT create a BusinessProfile.
    /// **Validates: Requirements 4.8, 4.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(OversizedLogoArbitrary) })]
    public Property LogoExceeding2MB_IsRejected(OversizedLogoInput input)
    {
        var (service, dbContext) = CreateService();

        try
        {
            var logoFile = CreateMockFile("image/png", input.FileSizeBytes, "logo.png");

            var model = new SetupWizardModel
            {
                BusinessName = "Valid Business Name",
                CurrencySymbol = "€",
                Logo = logoFile
            };

            var result = service.CompleteSetupAsync(TestBusinessId, model).Result;

            var noProfileCreated = !dbContext.BusinessProfiles.Any(bp => bp.BusinessId == TestBusinessId);

            return (!result.Success
                && result.ValidationErrors.ContainsKey("Logo")
                && noProfileCreated
                && input.FileSizeBytes > 2 * 1024 * 1024).ToProperty()
                .Label($"FileSize={input.FileSizeBytes} bytes ({input.FileSizeBytes / 1024.0 / 1024.0:F2}MB): " +
                       $"Expected rejection, Got Success={result.Success}, ProfileCreated={!noProfileCreated}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}

#region Custom Arbitraries for Setup Wizard Input Validation

/// <summary>
/// Input for testing invalid (empty/whitespace/null) business names.
/// </summary>
public class InvalidBusinessNameInput
{
    public string? BusinessName { get; set; }

    public override string ToString() => $"(BusinessName='{BusinessName}')";
}

/// <summary>
/// Arbitrary that generates empty, whitespace-only, or null business names.
/// </summary>
public class InvalidBusinessNameArbitrary
{
    public static Arbitrary<InvalidBusinessNameInput> InvalidBusinessNameInputs()
    {
        var gen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   ", " \t\n ");

        return Arb.From(gen.Select(name => new InvalidBusinessNameInput { BusinessName = name }));
    }
}

/// <summary>
/// Input for testing business names exceeding 200 characters.
/// </summary>
public class TooLongBusinessNameInput
{
    public string BusinessName { get; set; } = null!;

    public override string ToString() => $"(BusinessName length={BusinessName.Length})";
}

/// <summary>
/// Arbitrary that generates business names exceeding 200 characters.
/// </summary>
public class TooLongBusinessNameArbitrary
{
    public static Arbitrary<TooLongBusinessNameInput> TooLongBusinessNameInputs()
    {
        var gen = Gen.Choose(201, 500).Select(length =>
            new TooLongBusinessNameInput
            {
                BusinessName = new string('A', length)
            });

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing VAT numbers exceeding 50 characters.
/// </summary>
public class TooLongVatNumberInput
{
    public string VatNumber { get; set; } = null!;

    public override string ToString() => $"(VatNumber length={VatNumber.Length})";
}

/// <summary>
/// Arbitrary that generates VAT numbers exceeding 50 characters.
/// </summary>
public class TooLongVatNumberArbitrary
{
    public static Arbitrary<TooLongVatNumberInput> TooLongVatNumberInputs()
    {
        var gen = Gen.Choose(51, 200).Select(length =>
            new TooLongVatNumberInput
            {
                VatNumber = "IE" + new string('0', length - 2)
            });

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing invalid (empty/whitespace/null) currency symbols.
/// </summary>
public class InvalidCurrencyInput
{
    public string? CurrencySymbol { get; set; }

    public override string ToString() => $"(CurrencySymbol='{CurrencySymbol}')";
}

/// <summary>
/// Arbitrary that generates empty, whitespace-only, or null currency symbols.
/// </summary>
public class InvalidCurrencyArbitrary
{
    public static Arbitrary<InvalidCurrencyInput> InvalidCurrencyInputs()
    {
        var gen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n");

        return Arb.From(gen.Select(symbol => new InvalidCurrencyInput { CurrencySymbol = symbol }));
    }
}

/// <summary>
/// Input for testing invalid logo file content types.
/// </summary>
public class InvalidLogoTypeInput
{
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string FileName { get; set; } = null!;

    public override string ToString() => $"(ContentType='{ContentType}', Size={FileSizeBytes})";
}

/// <summary>
/// Arbitrary that generates invalid logo file content types (not PNG/JPG/SVG).
/// </summary>
public class InvalidLogoTypeArbitrary
{
    public static Arbitrary<InvalidLogoTypeInput> InvalidLogoTypeInputs()
    {
        var invalidContentTypes = Gen.Elements(
            "application/pdf",
            "image/gif",
            "image/bmp",
            "image/webp",
            "image/tiff",
            "text/plain",
            "application/octet-stream",
            "video/mp4",
            "application/zip",
            "text/html");

        var fileSizeGen = Gen.Choose(1, 2 * 1024 * 1024); // Valid size (under 2MB)

        var gen = from contentType in invalidContentTypes
                  from size in fileSizeGen
                  select new InvalidLogoTypeInput
                  {
                      ContentType = contentType,
                      FileSizeBytes = size,
                      FileName = $"logo.{contentType.Split('/').Last()}"
                  };

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing logo files exceeding 2MB.
/// </summary>
public class OversizedLogoInput
{
    public long FileSizeBytes { get; set; }

    public override string ToString() => $"(FileSize={FileSizeBytes} bytes, {FileSizeBytes / 1024.0 / 1024.0:F2}MB)";
}

/// <summary>
/// Arbitrary that generates file sizes exceeding 2MB (2,097,152 bytes).
/// </summary>
public class OversizedLogoArbitrary
{
    public static Arbitrary<OversizedLogoInput> OversizedLogoInputs()
    {
        // Generate sizes from just over 2MB to 10MB
        var gen = Gen.Choose(2 * 1024 * 1024 + 1, 10 * 1024 * 1024).Select(size =>
            new OversizedLogoInput
            {
                FileSizeBytes = size
            });

        return Arb.From(gen);
    }
}

#endregion
