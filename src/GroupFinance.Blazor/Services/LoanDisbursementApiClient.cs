using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class LoanDisbursementRequestModel
{
    public long FinancialAccountId { get; set; }
    public DateTime DisbursementDate { get; set; } = DateTime.Today;
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public sealed record LoanDisbursementModel(
    long LoanId, long LoanApplicationId, string LoanNumber, string LoanApplicationNumber,
    long GroupId, string GroupCode, long MemberId, string MemberNumber, string MemberName,
    DateTime DisbursementDate, decimal PrincipalAmount, decimal InterestRate, string InterestMethod,
    int DurationMonths, string InstallmentFrequency, decimal MonthlyInstallment, decimal TotalInterest,
    decimal TotalRepayment, string Status, long FinancialTransactionId, string FinancialTransactionNumber,
    long FinancialAccountId, string FinancialAccountCode, string FinancialAccountName, int ScheduleCount);

public sealed record LoanSummaryModel(
    long Id, long GroupId, long MemberId, string MemberNumber, string MemberName, long LoanApplicationId,
    string LoanNumber, DateTime DisbursementDate, decimal PrincipalAmount, decimal InterestRate,
    string InterestMethod, int DurationMonths, decimal InstallmentAmount, decimal TotalInterest,
    decimal TotalRepayment, decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid,
    decimal OverpaymentAmount, decimal OutstandingPrincipal, string Status);

public sealed record LoanScheduleModel(
    long Id, long LoanId, int InstallmentNumber, DateTime DueDate, decimal OpeningPrincipal,
    decimal PrincipalDue, decimal InterestDue, decimal PenaltyDue, decimal TotalDue,
    decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid, decimal OutstandingAmount, string Status);

public sealed class LoanDisbursementApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, LoanDisbursementModel? Data)> DisburseAsync(long applicationId, LoanDisbursementRequestModel model)
        => await SendAsync<LoanDisbursementModel>(HttpMethod.Post, $"api/loan-disbursements/{applicationId}", model);

    public async Task<(bool Success, string? Error, IReadOnlyList<LoanSummaryModel>? Data)> GetLoansAsync(long groupId, string? status = null, long? memberId = null)
    {
        var uri = $"api/loan-disbursements/loans?groupId={groupId}";
        if (!string.IsNullOrWhiteSpace(status)) uri += $"&status={Uri.EscapeDataString(status)}";
        if (memberId.HasValue) uri += $"&memberId={memberId.Value}";
        var result = await SendAsync<List<LoanSummaryModel>>(HttpMethod.Get, uri);
        return (result.Success, result.Error, result.Data);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<LoanScheduleModel>? Data)> GetScheduleAsync(long loanId)
    {
        var result = await SendAsync<List<LoanScheduleModel>>(HttpMethod.Get, $"api/loan-disbursements/loans/{loanId}/schedule");
        return (result.Success, result.Error, result.Data);
    }

    private async Task<(bool Success, string? Error, T? Data)> SendAsync<T>(HttpMethod method, string uri, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
        return (true, null, await response.Content.ReadFromJsonAsync<T>());
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try { var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}."; }
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record ErrorResponse(string? Message);
}
