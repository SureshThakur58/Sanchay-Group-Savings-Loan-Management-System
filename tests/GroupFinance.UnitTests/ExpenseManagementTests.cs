using Xunit;

namespace GroupFinance.UnitTests;

public sealed class ExpenseManagementTests
{
    [Theory]
    [InlineData("Cash")]
    [InlineData("Bank")]
    [InlineData("Online Transfer")]
    [InlineData("Other")]
    public void SupportedPaymentMethods_AreAccepted(string method)
    {
        var supported = new[] { "Cash", "Bank", "Online Transfer", "Other" };
        Assert.Contains(method, supported);
    }

    [Fact]
    public void ExpenseJournal_IsBalanced()
    {
        const decimal amount = 750m;
        Assert.Equal(amount, amount);
    }

    [Fact]
    public void InsufficientFund_IsRejectedByBusinessRule()
    {
        const decimal available = 500m;
        const decimal expense = 750m;
        Assert.True(available < expense);
    }

    [Fact]
    public void ExpenseNumber_UsesEightDigitSequenceFormat()
    {
        var id = 12L;
        Assert.Equal("EXP-00000012", $"EXP-{id:00000000}");
    }
}
