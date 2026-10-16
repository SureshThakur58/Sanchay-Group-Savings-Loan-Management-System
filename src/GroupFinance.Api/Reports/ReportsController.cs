using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupFinance.Core.Security;

namespace GroupFinance.Api.Reports;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = PermissionCodes.ReportsView)]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<ReportSummaryResponse> Summary(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetSummaryAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("members")]
    public Task<IReadOnlyCollection<MemberReportResponse>> Members(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetMembersAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("contributions")]
    public Task<IReadOnlyCollection<ContributionReportResponse>> Contributions(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId, CancellationToken ct) =>
        reports.GetContributionsAsync(groupId, fromDate, toDate, memberId, ct);

    [HttpGet("collection-due")]
    public Task<IReadOnlyCollection<CollectionDueReportResponse>> CollectionDue(long groupId, DateTime fromDate, DateTime toDate, CancellationToken ct) =>
        reports.GetCollectionDueAsync(groupId, fromDate, toDate, ct);

    [HttpGet("collection-outstanding")]
    public Task<IReadOnlyCollection<CollectionDueReportResponse>> CollectionOutstanding(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetCollectionOutstandingAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("loan-register")]
    public Task<IReadOnlyCollection<LoanRegisterReportResponse>> LoanRegister(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetLoanRegisterAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("loan-outstanding")]
    public Task<IReadOnlyCollection<LoanRegisterReportResponse>> LoanOutstanding(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetLoanOutstandingAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("loan-repayments")]
    public Task<IReadOnlyCollection<LoanRepaymentReportResponse>> LoanRepayments(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId, long? loanId, CancellationToken ct) =>
        reports.GetLoanRepaymentsAsync(groupId, fromDate, toDate, memberId, loanId, ct);

    [HttpGet("loan-installments")]
    public Task<IReadOnlyCollection<LoanInstallmentReportResponse>> LoanInstallments(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId, CancellationToken ct) =>
        reports.GetLoanInstallmentsAsync(groupId, fromDate, toDate, loanId, ct);

    [HttpGet("overdue")]
    public Task<IReadOnlyCollection<OverdueReportResponse>> Overdue(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetOverdueAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("interest-income")]
    public Task<IReadOnlyCollection<InterestIncomeReportResponse>> InterestIncome(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId, CancellationToken ct) =>
        reports.GetInterestIncomeAsync(groupId, fromDate, toDate, loanId, ct);

    [HttpGet("profit-distribution")]
    public Task<IReadOnlyCollection<ProfitDistributionReportResponse>> ProfitDistribution(long groupId, DateTime? fromDate, DateTime? toDate, CancellationToken ct) =>
        reports.GetProfitDistributionAsync(groupId, fromDate, toDate, ct);

    [HttpGet("expenses")]
    public Task<IReadOnlyCollection<ExpenseReportResponse>> Expenses(long groupId, DateTime? fromDate, DateTime? toDate, long? categoryId, CancellationToken ct) =>
        reports.GetExpensesAsync(groupId, fromDate, toDate, categoryId, ct);

    [HttpGet("assets")]
    public Task<IReadOnlyCollection<AssetReportResponse>> Assets(long groupId, DateTime? asOfDate, bool includeInactive, CancellationToken ct) =>
        reports.GetAssetsAsync(groupId, (asOfDate ?? DateTime.Today).Date, includeInactive, ct);

    [HttpGet("cash-bank")]
    public Task<IReadOnlyCollection<CashBankReportResponse>> CashBank(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetCashBankAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("income-expense")]
    public Task<IReadOnlyCollection<IncomeExpenseReportResponse>> IncomeExpense(long groupId, DateTime fromDate, DateTime toDate, CancellationToken ct) =>
        reports.GetIncomeExpenseAsync(groupId, fromDate, toDate, ct);

    [HttpGet("balance-sheet")]
    public Task<BalanceSheetResponse> BalanceSheet(long groupId, DateTime? asOfDate, CancellationToken ct) =>
        reports.GetBalanceSheetAsync(groupId, (asOfDate ?? DateTime.Today).Date, ct);

    [HttpGet("member-statement")]
    public async Task<IActionResult> MemberStatement(long groupId, long? memberId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        if (!memberId.HasValue || memberId.Value <= 0)
            return BadRequest(new { message = "memberId is required for the member statement report." });

        try
        {
            var result = await reports.GetMemberStatementAsync(groupId, memberId.Value, fromDate, toDate, ct);
            return Ok(result);
        }
        catch (ReportValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
