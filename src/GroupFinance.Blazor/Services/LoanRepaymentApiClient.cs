using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class LoanRepaymentRequestModel
{
    public long FinancialAccountId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public sealed record LoanRepaymentAllocationModel(long LoanScheduleId, int InstallmentNumber, DateTime DueDate, decimal PenaltyAllocated, decimal InterestAllocated, decimal PrincipalAllocated);
public sealed record LoanRepaymentModel(long LoanPaymentId, long LoanId, string LoanNumber, long MemberId, decimal Amount, decimal PenaltyAllocated, decimal InterestAllocated, decimal PrincipalAllocated, decimal OverpaymentAmount, string PaymentMethod, DateTime TransactionDate, string Status, long FinancialTransactionId, string FinancialTransactionNumber, long FinancialAccountId, string FinancialAccountCode, string FinancialAccountName, decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid, decimal OutstandingPrincipal, string LoanStatus, IReadOnlyList<LoanRepaymentAllocationModel> Allocations);
public sealed record LoanPaymentSummaryModel(long Id, long LoanId, string LoanNumber, long MemberId, DateTime TransactionDate, decimal Amount, decimal PenaltyAllocated, decimal InterestAllocated, decimal PrincipalAllocated, decimal OverpaymentAmount, string PaymentMethod, string? ReferenceNumber, string? Description, string Status, long? FinancialTransactionId);

public sealed class LoanRepaymentApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, LoanRepaymentModel? Data)> RepayAsync(long loanId, LoanRepaymentRequestModel model)
        => SendAsync<LoanRepaymentRequestModel, LoanRepaymentModel>(HttpMethod.Post, $"api/loan-repayments/{loanId}", model);

    public async Task<(bool Success, string? Error, IReadOnlyList<LoanPaymentSummaryModel>? Data)> GetPaymentsAsync(long loanId)
    {
        var result = await SendGetAsync<List<LoanPaymentSummaryModel>>($"api/loan-repayments/{loanId}/payments");
        return (result.Success, result.Error, result.Data);
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
    private sealed record ErrorResponse(string? Message);
}
