using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.Web.Services.Billing;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for VatCalculationService covering edge cases in VAT rate determination.
/// Validates Requirements 9.1, 9.2, 9.3, 9.4, 9.5.
/// </summary>
public class VatCalculationServiceTests
{
    private readonly VatCalculationService _service;

    public VatCalculationServiceTests()
    {
        _service = new VatCalculationService(NullLogger<VatCalculationService>.Instance);
    }

    #region Null country defaults to 19% (Req 9.5)

    [Fact]
    public void Calculate_NullCountry_DefaultsToCyprusVatRate()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, null, null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.Equal(119m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    #endregion

    #region Empty string country defaults to 19% (Req 9.5)

    [Fact]
    public void Calculate_EmptyStringCountry_DefaultsToCyprusVatRate()
    {
        // Arrange
        var netAmount = 200m;

        // Act
        var result = _service.Calculate(netAmount, "", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(38m, result.VatAmount);
        Assert.Equal(238m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    [Fact]
    public void Calculate_WhitespaceCountry_DefaultsToCyprusVatRate()
    {
        // Arrange
        var netAmount = 50m;

        // Act
        var result = _service.Calculate(netAmount, "   ", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(9.50m, result.VatAmount);
        Assert.Equal(59.50m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
    }

    #endregion

    #region Cyprus customer gets 19% (Req 9.1)

    [Fact]
    public void Calculate_CyprusCustomer_AppliesDomesticVatRate()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.Equal(119m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    [Fact]
    public void Calculate_CyprusCustomerWithVatNumber_StillApplies19Percent()
    {
        // Arrange — even with VAT number, domestic CY customer pays 19%
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "CY", "CY12345678X");

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.False(result.IsReverseCharge);
    }

    #endregion

    #region EU customer with VAT number gets 0% reverse charge (Req 9.2)

    [Fact]
    public void Calculate_EuCustomerWithVatNumber_AppliesReverseCharge()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "DE", "DE123456789");

        // Assert
        Assert.Equal(0m, result.VatRate);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(100m, result.GrossAmount);
        Assert.True(result.IsReverseCharge);
        Assert.Equal("Reverse Charge - Article 196 Council Directive 2006/112/EC", result.ReverseChargeNotation);
    }

    [Theory]
    [InlineData("FR", "FR12345678901")]
    [InlineData("NL", "NL123456789B01")]
    [InlineData("IT", "IT12345678901")]
    [InlineData("ES", "ESA12345678")]
    public void Calculate_VariousEuCountriesWithVatNumber_AppliesReverseCharge(string country, string vatNumber)
    {
        // Arrange
        var netAmount = 250m;

        // Act
        var result = _service.Calculate(netAmount, country, vatNumber);

        // Assert
        Assert.Equal(0m, result.VatRate);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(250m, result.GrossAmount);
        Assert.True(result.IsReverseCharge);
        Assert.NotNull(result.ReverseChargeNotation);
    }

    #endregion

    #region EU customer without VAT number gets 19% (Req 9.3)

    [Fact]
    public void Calculate_EuCustomerWithoutVatNumber_AppliesCyprusVatRate()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "DE", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.Equal(119m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    [Fact]
    public void Calculate_EuCustomerWithEmptyVatNumber_AppliesCyprusVatRate()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "FR", "");

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.Equal(119m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
    }

    [Fact]
    public void Calculate_EuCustomerWithWhitespaceVatNumber_AppliesCyprusVatRate()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "NL", "   ");

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
        Assert.False(result.IsReverseCharge);
    }

    #endregion

    #region Non-EU customer gets 0% with no reverse charge (Req 9.4)

    [Fact]
    public void Calculate_NonEuCustomer_AppliesZeroVatNoReverseCharge()
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "US", null);

        // Assert
        Assert.Equal(0m, result.VatRate);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(100m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("GB")]
    [InlineData("CH")]
    [InlineData("JP")]
    [InlineData("AU")]
    public void Calculate_VariousNonEuCountries_AppliesZeroVat(string country)
    {
        // Arrange
        var netAmount = 500m;

        // Act
        var result = _service.Calculate(netAmount, country, null);

        // Assert
        Assert.Equal(0m, result.VatRate);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(500m, result.GrossAmount);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    [Fact]
    public void Calculate_NonEuCustomerWithVatNumber_StillZeroVatNoReverseCharge()
    {
        // Arrange — non-EU customer with a VAT number should still get 0% and NOT be reverse charge
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, "GB", "GB123456789");

        // Assert
        Assert.Equal(0m, result.VatRate);
        Assert.False(result.IsReverseCharge);
        Assert.Null(result.ReverseChargeNotation);
    }

    #endregion

    #region Decimal precision and rounding behavior

    [Fact]
    public void Calculate_SmallAmount_MaintainsDecimalPrecision()
    {
        // Arrange — 0.01 * 0.19 = 0.0019
        var netAmount = 0.01m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(0.0019m, result.VatAmount);
        Assert.Equal(0.0119m, result.GrossAmount);
    }

    [Fact]
    public void Calculate_LargeAmount_MaintainsDecimalPrecision()
    {
        // Arrange — 99999.99 * 0.19 = 18999.9981
        var netAmount = 99999.99m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(18999.9981m, result.VatAmount);
        Assert.Equal(118999.9881m, result.GrossAmount);
    }

    [Fact]
    public void Calculate_ZeroAmount_ProducesZeroVatAndGross()
    {
        // Arrange
        var netAmount = 0m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(0m, result.GrossAmount);
    }

    [Fact]
    public void Calculate_FractionalAmount_ProducesAccurateResults()
    {
        // Arrange — 33.33 * 0.19 = 6.3327
        var netAmount = 33.33m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert
        Assert.Equal(6.3327m, result.VatAmount);
        Assert.Equal(39.6627m, result.GrossAmount);
    }

    [Fact]
    public void Calculate_GrossAmountEqualsNetPlusVat()
    {
        // Arrange
        var netAmount = 157.89m;

        // Act
        var result = _service.Calculate(netAmount, "CY", null);

        // Assert — gross = net + vat always
        Assert.Equal(netAmount + result.VatAmount, result.GrossAmount);
    }

    #endregion

    #region Case insensitivity for country codes

    [Theory]
    [InlineData("cy")]
    [InlineData("Cy")]
    [InlineData("cY")]
    [InlineData("CY")]
    public void Calculate_CyprusCountryCodeCaseInsensitive_AppliesDomesticVat(string country)
    {
        // Arrange
        var netAmount = 100m;

        // Act
        var result = _service.Calculate(netAmount, country, null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
        Assert.Equal(19m, result.VatAmount);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("De")]
    [InlineData("DE")]
    public void Calculate_EuCountryCodeCaseInsensitive_AppliesCorrectRate(string country)
    {
        // Arrange
        var netAmount = 100m;

        // Act — EU country without VAT number → 19%
        var result = _service.Calculate(netAmount, country, null);

        // Assert
        Assert.Equal(0.19m, result.VatRate);
    }

    #endregion
}
