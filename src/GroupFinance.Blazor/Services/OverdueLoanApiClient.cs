using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record OverdueEvaluationModel(long GroupId, DateTime AsOfDate, int OverdueInstallmentCount, int OverdueLoanCount, decimal OutstandingPrincipal, decimal OutstandingInterest, decimal OutstandingPenalty, decimal TotalOverdueAmount);
public sealed record OverdueLoanScheduleModel(long LoanScheduleId, long LoanId, string LoanNumber, long MemberId, string MemberNumber, string MemberName, int InstallmentNumber, DateTime DueDate, int DaysOverdue, decimal PrincipalDue, decimal InterestDue, decimal PenaltyDue, decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid, decimal OutstandingPrincipal, decimal OutstandingInterest, decimal OutstandingPenalty, decimal OutstandingAmount, string Status);
public sealed record OverdueLoanSummaryModel(long LoanId, string LoanNumber, long GroupId, long MemberId, string MemberNumber, string MemberName, int OverdueInstallmentCount, int MaximumDaysOverdue, decimal OutstandingPrincipal, decimal OutstandingInterest, decimal OutstandingPenalty, decimal TotalOverdueAmount, DateTime EarliestDueDate, string Status, IReadOnlyList<OverdueLoanScheduleModel> Installments);

public sealed class OverdueLoanApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, OverdueEvaluationModel? Data)> EvaluateAsync(long groupId, DateTime asOfDate)
        => SendAsync<EvaluateRequest, OverdueEvaluationModel>(HttpMethod.Post, "api/overdue-loans/evaluate", new EvaluateRequest(groupId, asOfDate.Date));

    public async Task<(bool Success, string? Error, IReadOnlyList<OverdueLoanSummaryModel>? Data)> GetOverdueLoansAsync(long groupId, DateTime? asOfDate = null)
    {
        var query = $"api/overdue-loans?groupId={groupId}";
        if (asOfDate.HasValue) query += $"&asOfDate={Uri.EscapeDataString(asOfDate.Value.Date.ToString("yyyy-MM-dd"))}";
        var result = await SendGetAsync<List<OverdueLoanSummaryModel>>(query);
        return (result.Success, result.Error, result.Data);
    }

    public async Task<(bool Success, string? Error, OverdueLoanSummaryModel? Data)> GetLoanAsync(long loanId, DateTime? asOfDate = null)
    {
        var query = $"api/overdue-loans/loans/{loanId}";
        if (asOfDate.HasValue) query += $"?asOfDate={Uri.EscapeDataString(asOfDate.Value.Date.ToString("yyyy-MM-dd"))}";
        return await SendGetAsync<OverdueLoanSummaryModel>(query);
    }

    private async Task<(bool Success, string? Error, TResponse? Data)> SendAsync<TRequest, TResponse>(HttpMethod method, string uri, TRequest model)
    {
        using var request = CreateRequest(method, uri);
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        return await ReadResponseAsync<TResponse>(response);
    }

    private async Task<(bool Success, string? Error, T? Data)> SendGetAsync<T>(string uri)
    {
        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await http.SendAsync(request);
        return await ReadResponseAsync<T>(response);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        return request;
    }

    private static async Task<(bool Success, string? Error, T? Data)> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
        var data = await response.Content.ReadFromJsonAsync<T>();
        return data is null ? (false, "The API returned an invalid response.", default) : (true, null, data);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try { var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}."; }
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record EvaluateRequest(long GroupId, DateTime AsOfDate);
    private sealed record ErrorResponse(string? Message);
}
