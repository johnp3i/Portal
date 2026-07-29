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
        var original = "sk_test_4eC39HqLyjWDarjtT1zdp7dc";
        var encrypted = _service.Encrypt(original);
        var decrypted = _service.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext()
    {
        var original = "sk_test_abc123";
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
        var result = _service.Mask("sk_test_4eC39HqLyjWDarjtT1zdp7dc");
        Assert.StartsWith("sk_test_", result);
        Assert.EndsWith("p7dc", result);
        Assert.Contains("****", result);
    }

    [Fact]
    public void Mask_ConnectClientId_ShowsPrefixAndLastFour()
    {
        var result = _service.Mask("ca_ABC123XYZ789");
        Assert.StartsWith("ca_", result);
        Assert.EndsWith("Z789", result);
        Assert.Contains("****", result);
    }

    [Fact]
    public void Mask_WebhookSecret_ShowsPrefixAndLastFour()
    {
        var result = _service.Mask("whsec_testvalue12345abcdef");
        Assert.Contains("****", result);
        Assert.EndsWith("cdef", result);
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
