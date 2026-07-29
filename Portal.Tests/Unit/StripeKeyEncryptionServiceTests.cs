using Microsoft.AspNetCore.DataProtection;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.Unit;

public class StripeKeyEncryptionServiceTests
{
    private readonly StripeKeyEncryptionService _service;

    public StripeKeyEncryptionServiceTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        _service = new StripeKeyEncryptionService(provider);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_ReturnsOriginalValue()
    {
        var original = "sk_test_FAKE_KEY_FOR_UNIT_TEST_ONLY";
        var encrypted = _service.Encrypt(original);
        var decrypted = _service.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext()
    {
        var original = "sk_test_FAKE_NOT_REAL_12345";
        var encrypted = _service.Encrypt(original);
        Assert.NotEqual(original, encrypted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encrypt_NullOrEmpty_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => _service.Encrypt(input!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_NullOrEmpty_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => _service.Decrypt(input!));
    }

    [Fact]
    public void Mask_StandardSecretKey_ShowsPrefixAndLastFour()
    {
        var result = _service.Mask("sk_test_FAKE_KEY_FOR_UNIT_TEST_ONLY");
        Assert.StartsWith("sk_test_", result);
        Assert.EndsWith("ONLY", result);
        Assert.Contains("****", result);
    }

    [Fact]
    public void Mask_ConnectClientId_ShowsPrefixAndLastFour()
    {
        var result = _service.Mask("ca_FAKE_TEST_VALUE_1234");
        Assert.StartsWith("ca_", result);
        Assert.EndsWith("1234", result);
        Assert.Contains("****", result);
    }

    [Fact]
    public void Mask_WebhookSecret_ShowsPrefixAndLastFour()
    {
        var result = _service.Mask("whsec_FAKE_TEST_VALUE_ONLY");
        Assert.Contains("****", result);
        Assert.EndsWith("ONLY", result);
    }

    [Fact]
    public void Mask_ShortKey_DoesNotCrash()
    {
        var result = _service.Mask("abc");
        Assert.NotNull(result);
        Assert.Contains("****", result);
    }

    [Fact]
    public void Mask_EmptyString_ReturnsMaskedPlaceholder()
    {
        var result = _service.Mask("");
        Assert.Equal("****", result);
    }

    [Fact]
    public void Mask_Null_ReturnsMaskedPlaceholder()
    {
        var result = _service.Mask(null!);
        Assert.Equal("****", result);
    }
}
