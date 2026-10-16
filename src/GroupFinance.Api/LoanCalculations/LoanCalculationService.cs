using GroupFinance.Core.Loans;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.LoanCalculations;

public sealed class ApiLoanCalculationService(GroupFinanceDbContext db, ILoanCalculationService engine)
{
    public async Task<LoanCalculationResponse> CalculateAsync(LoanCalculationRequest request, CancellationToken cancellationToken)
    {
        if (request.GroupId <= 0)
            throw new ArgumentException("Group ID must be greater than zero.", nameof(request));

        var settings = await db.GroupSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == request.GroupId, cancellationToken);
        if (settings is null)
            throw new ArgumentException("The selected group does not have loan settings configured.", nameof(request));

        var result = engine.Calculate(new GroupFinance.Core.Loans.LoanCalculationRequest(
            request.PrincipalAmount,
            request.AnnualInterestRate,
            request.DurationMonths,
            request.InterestMethod,
            request.FirstDueDate,
            settings.MonetaryPrecision));

        return new LoanCalculationResponse(
            result.PrincipalAmount,
            result.AnnualInterestRate,
            result.DurationMonths,
            result.InterestMethod,
            result.MonthlyInstallment,
            result.TotalInterest,
            result.TotalRepayment,
            result.Schedule.Select(x => new LoanScheduleLineResponse(
                x.InstallmentNumber,
                x.DueDate,
                x.OpeningPrincipal,
                x.PrincipalDue,
                x.InterestDue,
                x.TotalDue,
                x.ClosingPrincipal)).ToArray());
    }
}
