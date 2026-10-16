namespace GroupFinance.Core.InterestProfit;

public sealed record ProfitLineInput(
    string AccountCode,
    string AccountType,
    string TransactionType,
    decimal DebitAmount,
    decimal CreditAmount);

public sealed record ProfitCalculationResult(
    decimal LoanInterestIncome,
    decimal InvestmentIncome,
    decimal OtherIncome,
    decimal TotalIncome,
    decimal Expenses,
    decimal OtherCosts,
    decimal TotalCosts,
    decimal NetProfit,
    decimal ReserveAmount,
    decimal DistributableAmount);

public static class ProfitCalculationEngine
{
    public static ProfitCalculationResult Calculate(
        IEnumerable<ProfitLineInput> lines,
        decimal reserveRate,
        int precision)
    {
        if (reserveRate < 0m || reserveRate > 100m)
            throw new ArgumentException("Reserve rate must be between 0 and 100 percent.", nameof(reserveRate));
        if (precision < 0 || precision > 6)
            throw new ArgumentOutOfRangeException(nameof(precision));

        decimal loanInterest = 0m;
        decimal investment = 0m;
        decimal otherIncome = 0m;
        decimal expenses = 0m;
        decimal otherCosts = 0m;

        foreach (var line in lines)
        {
            var type = line.AccountType?.Trim() ?? string.Empty;
            var code = line.AccountCode?.Trim() ?? string.Empty;
            var transactionType = line.TransactionType?.Trim() ?? string.Empty;

            if (type.Equals("Income", StringComparison.OrdinalIgnoreCase))
            {
                var amount = Math.Max(0m, line.CreditAmount - line.DebitAmount);
                if (amount == 0m) continue;

                if (code.Equals("LOAN-INTEREST-INCOME", StringComparison.OrdinalIgnoreCase))
                    loanInterest += amount;
                else if (code.StartsWith("INVESTMENT-", StringComparison.OrdinalIgnoreCase))
                    investment += amount;
                else
                    otherIncome += amount;
            }
            else if (type.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            {
                var amount = Math.Max(0m, line.DebitAmount - line.CreditAmount);
                if (amount == 0m) continue;

                if (transactionType.Equals("EXPENSE", StringComparison.OrdinalIgnoreCase))
                    expenses += amount;
                else
                    otherCosts += amount;
            }
        }

        loanInterest = RoundMoney(loanInterest, precision);
        investment = RoundMoney(investment, precision);
        otherIncome = RoundMoney(otherIncome, precision);
        expenses = RoundMoney(expenses, precision);
        otherCosts = RoundMoney(otherCosts, precision);

        var totalIncome = RoundMoney(loanInterest + investment + otherIncome, precision);
        var totalCosts = RoundMoney(expenses + otherCosts, precision);
        var netProfit = RoundMoney(totalIncome - totalCosts, precision);
        var reserve = netProfit > 0m
            ? RoundMoney(netProfit * reserveRate / 100m, precision)
            : 0m;
        var distributable = RoundMoney(netProfit - reserve, precision);

        return new ProfitCalculationResult(
            loanInterest, investment, otherIncome, totalIncome,
            expenses, otherCosts, totalCosts, netProfit,
            reserve, distributable);
    }

    private static decimal RoundMoney(decimal value, int precision) =>
        Math.Round(value, precision, MidpointRounding.AwayFromZero);
}
