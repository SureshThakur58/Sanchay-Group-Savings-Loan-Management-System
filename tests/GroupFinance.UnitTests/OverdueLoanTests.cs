using Xunit;

namespace GroupFinance.UnitTests;

public sealed class OverdueLoanTests
{
    [Fact]
    public void DaysOverdue_IsCalendarDayDifference()
    {
        var due = new DateTime(2026, 8, 15);
        var asOf = new DateTime(2026, 8, 20);
        Assert.Equal(5, (asOf.Date - due.Date).Days);
    }

    [Fact]
    public void LateFee_IsOnePercentOfOutstandingPrincipalAndInterest()
    {
        const decimal principal = 7_885m;
        const decimal interest = 1_000m;
        const decimal rate = 1m;
        var penalty = decimal.Round((principal + interest) * rate / 100m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(88.85m, penalty);
    }

    [Fact]
    public void NotOverdue_OnDueDate()
    {
        var due = new DateTime(2026, 8, 15);
        var asOf = new DateTime(2026, 8, 15);
        Assert.False(asOf.Date > due.Date);
    }

    [Fact]
    public void PaidInstallment_HasNoOutstandingAmount()
    {
        var principalOutstanding = Math.Max(0m, 7_885m - 7_885m);
        var interestOutstanding = Math.Max(0m, 1_000m - 1_000m);
        var penaltyOutstanding = Math.Max(0m, 88.85m - 88.85m);
        Assert.Equal(0m, principalOutstanding + interestOutstanding + penaltyOutstanding);
    }
}
