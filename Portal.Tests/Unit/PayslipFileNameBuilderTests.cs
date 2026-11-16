using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit;

public class PayslipFileNameBuilderTests
{
    [Fact]
    public void Build_FirstLast_ProducesSurnameFirstFormat()
    {
        Assert.Equal("Payslip_2026-10_Papamichael_Giannis.pdf",
            PayslipFileNameBuilder.Build("Giannis Papamichael", 2026, 10));
    }

    [Fact]
    public void Build_PadsSingleDigitMonth()
    {
        Assert.Equal("Payslip_2026-03_Smith_John.pdf",
            PayslipFileNameBuilder.Build("John Smith", 2026, 3));
    }

    [Fact]
    public void Build_MultiWordFirstName_UsesLastWordAsSurname()
    {
        Assert.Equal("Payslip_2026-10_Souza_Maria_De.pdf",
            PayslipFileNameBuilder.Build("Maria De Souza", 2026, 10));
    }

    [Fact]
    public void Build_SingleName_UsesItAsSurname()
    {
        Assert.Equal("Payslip_2026-10_Madonna.pdf",
            PayslipFileNameBuilder.Build("Madonna", 2026, 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_EmptyName_FallsBackToEmployee(string? name)
    {
        Assert.Equal("Payslip_2026-10_Employee.pdf", PayslipFileNameBuilder.Build(name, 2026, 10));
    }

    [Fact]
    public void Build_StripsUnsafeCharacters()
    {
        var result = PayslipFileNameBuilder.Build("John ../O'Neil:*?", 2026, 10);
        Assert.Equal("Payslip_2026-10_ONeil_John.pdf", result);
    }

    [Fact]
    public void Build_KeepsGreekLettersAndHyphenatedSurname()
    {
        Assert.Equal("Payslip_2026-10_Παπαμιχαήλ-Ιωάννου_Γιάννης.pdf",
            PayslipFileNameBuilder.Build("Γιάννης Παπαμιχαήλ-Ιωάννου", 2026, 10));
    }
}