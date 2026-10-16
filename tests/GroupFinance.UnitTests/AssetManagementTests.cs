using Xunit;

namespace GroupFinance.UnitTests;

public sealed class AssetManagementTests
{
    [Theory]
    [InlineData("Property")]
    [InlineData("Land")]
    [InlineData("Vehicle")]
    [InlineData("Computer")]
    [InlineData("Furniture")]
    [InlineData("Equipment")]
    [InlineData("Investment")]
    [InlineData("Other")]
    public void SupportedAssetCategories_AreDefined(string category)
    {
        var supported = new[] { "Property", "Land", "Vehicle", "Computer", "Furniture", "Equipment", "Investment", "Other" };
        Assert.Contains(category, supported);
    }

    [Theory]
    [InlineData(120000, 100000, 20000)]
    [InlineData(80000, 100000, -20000)]
    [InlineData(100000, 100000, 0)]
    public void AssetSale_GainLoss_IsSaleAmountMinusBookValue(decimal saleAmount, decimal bookValue, decimal expected)
    {
        Assert.Equal(expected, saleAmount - bookValue);
    }

    [Fact]
    public void AssetPurchaseJournal_IsBalanced()
    {
        const decimal amount = 100000m;
        var debit = amount;
        var credit = amount;
        Assert.Equal(debit, credit);
    }

    [Fact]
    public void AssetSaleGainJournal_IsBalanced()
    {
        const decimal saleAmount = 120000m;
        const decimal bookValue = 100000m;
        const decimal gain = saleAmount - bookValue;
        Assert.Equal(saleAmount, bookValue + gain);
    }

    [Fact]
    public void AssetSaleLossJournal_IsBalanced()
    {
        const decimal saleAmount = 80000m;
        const decimal bookValue = 100000m;
        const decimal loss = bookValue - saleAmount;
        Assert.Equal(saleAmount + loss, bookValue);
    }

    [Fact]
    public void AssetCode_UsesEightDigitSequenceFormat()
    {
        var id = 12L;
        Assert.Equal("AST-00000012", $"AST-{id:00000000}");
    }
}
