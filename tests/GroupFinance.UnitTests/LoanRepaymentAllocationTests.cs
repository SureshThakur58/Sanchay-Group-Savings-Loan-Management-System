using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanRepaymentAllocationTests
{
    [Fact]
    public void DuePayment_AllocatesPenaltyThenInterestThenPrincipal()
    {
        var schedules = new[]
        {
            new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 13), 200m, 50m, 10m)
        };

        var result = LoanRepaymentAllocationCalculator.Calculate(100m, new DateTime(2026, 9, 13), schedules);

        Assert.Equal(10m, result.PenaltyAllocated);
        Assert.Equal(50m, result.InterestAllocated);
        Assert.Equal(40m, result.PrincipalAllocated);
        Assert.Equal(0m, result.OverpaymentAmount);
    }

    [Fact]
    public void PartialPayment_CanBeAppliedAcrossMultipleDueSchedules()
    {
        var schedules = new[]
        {
            new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 13), 200m, 50m, 0m),
            new LoanRepaymentAllocationInput(2, 2, new DateTime(2026, 10, 13), 300m, 60m, 0m)
        };

        var result = LoanRepaymentAllocationCalculator.Calculate(250m, new DateTime(2026, 10, 13), schedules);

        Assert.Equal(0m, result.PenaltyAllocated);
        Assert.Equal(110m, result.InterestAllocated);
        Assert.Equal(140m, result.PrincipalAllocated);
        Assert.Equal(2, result.Allocations.Count);
    }

    [Fact]
    public void Prepayment_DoesNotCollectFutureInterest()
    {
        var schedules = new[]
        {
            new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 10, 13), 250m, 30m, 0m),
            new LoanRepaymentAllocationInput(2, 2, new DateTime(2026, 11, 13), 250m, 25m, 0m)
        };

        var result = LoanRepaymentAllocationCalculator.Calculate(100m, new DateTime(2026, 9, 13), schedules);

        Assert.Equal(0m, result.InterestAllocated);
        Assert.Equal(100m, result.PrincipalAllocated);
        Assert.Equal(0m, result.OverpaymentAmount);
        Assert.All(result.Allocations, x => Assert.Equal(0m, x.InterestAllocated));
    }

    [Fact]
    public void ExcessPayment_IsRetainedAsOverpayment()
    {
        var schedules = new[]
        {
            new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 13), 100m, 20m, 0m)
        };

        var result = LoanRepaymentAllocationCalculator.Calculate(150m, new DateTime(2026, 9, 13), schedules);

        Assert.Equal(20m, result.InterestAllocated);
        Assert.Equal(100m, result.PrincipalAllocated);
        Assert.Equal(30m, result.OverpaymentAmount);
    }
}
