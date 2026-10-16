using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanRepaymentBoundaryTests
{
    [Fact]
    public void Payment_On_Due_Date_Uses_Current_Installment()
    {
        var result = LoanRepaymentAllocationCalculator.Calculate(
            100m,
            new DateTime(2026, 9, 15),
            [new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 15), 80m, 20m, 0m)]);

        Assert.Equal(20m, result.InterestAllocated);
        Assert.Equal(80m, result.PrincipalAllocated);
        Assert.Equal(0m, result.OverpaymentAmount);
    }

    [Fact]
    public void Payment_Before_Due_Date_Does_Not_Collect_Future_Interest()
    {
        var result = LoanRepaymentAllocationCalculator.Calculate(
            50m,
            new DateTime(2026, 9, 14),
            [new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 15), 80m, 20m, 0m)]);

        Assert.Equal(0m, result.InterestAllocated);
        Assert.Equal(50m, result.PrincipalAllocated);
    }

    [Fact]
    public void Allocation_Total_Equals_Payment_When_No_Overpayment()
    {
        var result = LoanRepaymentAllocationCalculator.Calculate(
            250m,
            new DateTime(2026, 10, 15),
            [
                new LoanRepaymentAllocationInput(1, 1, new DateTime(2026, 9, 15), 100m, 25m, 5m),
                new LoanRepaymentAllocationInput(2, 2, new DateTime(2026, 10, 15), 100m, 20m, 0m)
            ]);

        Assert.Equal(250m, result.PenaltyAllocated + result.InterestAllocated + result.PrincipalAllocated + result.OverpaymentAmount);
    }
}
