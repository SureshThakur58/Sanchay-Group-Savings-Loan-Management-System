namespace GroupFinance.Core.Constants;

public static class GroupFundCalculation
{
    public static decimal TotalAvailableFund(decimal openingFund, decimal memberContributions, decimal loanInterest, decimal otherIncome, decimal investmentIncome) =>
        openingFund + memberContributions + loanInterest + otherIncome + investmentIncome;

    public static decimal ClosingFund(decimal totalAvailableFund, decimal loansGiven, decimal expenses, decimal assetPurchases, decimal withdrawals) =>
        totalAvailableFund - loansGiven - expenses - assetPurchases - withdrawals;
}
