namespace GroupFinance.Core.Loans;

/// <summary>
/// Central loan calculation engine. It is intentionally persistence-free so the same
/// calculation can be reused later by loan application, disbursement and repayment flows.
/// Monetary values are rounded to the group's configured financial precision.
/// </summary>
public sealed class LoanCalculationService : ILoanCalculationService
{

    public LoanCalculationResult Calculate(LoanCalculationRequest request)
    {
        Validate(request);

        if (request.MonetaryPrecision < 0 || request.MonetaryPrecision > 6)
            throw new ArgumentException("Monetary precision must be between 0 and 6.", nameof(request));

        var principal = RoundMoney(request.PrincipalAmount, request.MonetaryPrecision);
        var annualRate = request.AnnualInterestRate;
        var duration = request.DurationMonths;
        var method = NormalizeMethod(request.InterestMethod);
        var firstDueDate = request.FirstDueDate.Date;

        return method.Equals(LoanInterestMethods.Flat, StringComparison.OrdinalIgnoreCase)
            ? CalculateFlat(principal, annualRate, duration, firstDueDate, request.MonetaryPrecision)
            : CalculateReducingBalance(principal, annualRate, duration, firstDueDate, request.MonetaryPrecision);
    }

    private static LoanCalculationResult CalculateFlat(
        decimal principal,
        decimal annualRate,
        int duration,
        DateTime firstDueDate,
        int precision)
    {
        var totalInterest = RoundMoney(principal * annualRate / 100m * duration / 12m, precision);
        var totalRepayment = RoundMoney(principal + totalInterest, precision);
        var regularInstallment = RoundMoney(totalRepayment / duration, precision);
        var schedule = new List<LoanScheduleLine>(duration);
        var opening = principal;
        var accumulatedInterest = 0m;
        var accumulatedPrincipal = 0m;

        for (var i = 1; i <= duration; i++)
        {
            var interest = i == duration
                ? RoundMoney(totalInterest - accumulatedInterest, precision)
                : RoundMoney(totalInterest / duration, precision);

            var principalDue = i == duration
                ? RoundMoney(principal - accumulatedPrincipal, precision)
                : RoundMoney(regularInstallment - interest, precision);

            var installment = RoundMoney(principalDue + interest, precision);
            var closing = RoundMoney(opening - principalDue, precision);

            if (i == duration)
            {
                closing = 0m;
                principalDue = opening;
                installment = RoundMoney(principalDue + interest, precision);
            }

            schedule.Add(new LoanScheduleLine(
                i,
                firstDueDate.AddMonths(i - 1),
                opening,
                principalDue,
                interest,
                installment,
                closing));

            accumulatedInterest = RoundMoney(accumulatedInterest + interest, precision);
            accumulatedPrincipal = RoundMoney(accumulatedPrincipal + principalDue, precision);
            opening = closing;
        }

        return new LoanCalculationResult(
            principal,
            annualRate,
            duration,
            LoanInterestMethods.Flat,
            regularInstallment,
            RoundMoney(schedule.Sum(x => x.InterestDue), precision),
            RoundMoney(schedule.Sum(x => x.TotalDue), precision),
            schedule);
    }

    private static LoanCalculationResult CalculateReducingBalance(
        decimal principal,
        decimal annualRate,
        int duration,
        DateTime firstDueDate,
        int precision)
    {
        var monthlyRate = annualRate / 100m / 12m;
        var rawEmi = monthlyRate == 0m
            ? principal / duration
            : principal * monthlyRate * Pow(1m + monthlyRate, duration) /
              (Pow(1m + monthlyRate, duration) - 1m);
        var regularInstallment = RoundMoney(rawEmi, precision);

        var schedule = new List<LoanScheduleLine>(duration);
        var opening = principal;

        for (var i = 1; i <= duration; i++)
        {
            var interest = RoundMoney(opening * monthlyRate, precision);
            var principalDue = RoundMoney(regularInstallment - interest, precision);
            decimal installment;
            decimal closing;

            if (i == duration)
            {
                principalDue = RoundMoney(opening, precision);
                installment = RoundMoney(principalDue + interest, precision);
                closing = 0m;
            }
            else
            {
                if (principalDue > opening)
                    principalDue = opening;

                installment = RoundMoney(principalDue + interest, precision);
                closing = RoundMoney(opening - principalDue, precision);
            }

            schedule.Add(new LoanScheduleLine(
                i,
                firstDueDate.AddMonths(i - 1),
                opening,
                principalDue,
                interest,
                installment,
                closing));

            opening = closing;
        }

        return new LoanCalculationResult(
            principal,
            annualRate,
            duration,
            LoanInterestMethods.ReducingBalance,
            regularInstallment,
            RoundMoney(schedule.Sum(x => x.InterestDue), precision),
            RoundMoney(schedule.Sum(x => x.TotalDue), precision),
            schedule);
    }

    private static void Validate(LoanCalculationRequest request)
    {
        if (request.PrincipalAmount <= 0m)
            throw new ArgumentException("Loan principal must be greater than zero.", nameof(request));

        if (request.AnnualInterestRate < 0m || request.AnnualInterestRate > 100m)
            throw new ArgumentException("Annual interest rate must be between 0 and 100 percent.", nameof(request));

        if (request.DurationMonths <= 0)
            throw new ArgumentException("Loan duration must be greater than zero.", nameof(request));

        if (!LoanInterestMethods.IsSupported(request.InterestMethod))
            throw new ArgumentException("Interest method must be Flat or ReducingBalance.", nameof(request));

        if (request.FirstDueDate == default)
            throw new ArgumentException("First due date is required.", nameof(request));
    }

    private static string NormalizeMethod(string method) =>
        method.Equals(LoanInterestMethods.Flat, StringComparison.OrdinalIgnoreCase)
            ? LoanInterestMethods.Flat
            : LoanInterestMethods.ReducingBalance;

    private static decimal RoundMoney(decimal value, int precision) => Math.Round(value, precision, MidpointRounding.AwayFromZero);

    private static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
            result *= value;
        return result;
    }
}
