using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record GroupFundAccountModel(long Id, long GroupId, string AccountCode, string AccountName, string AccountType, bool IsSystemAccount, bool IsActive, decimal TotalDebit, decimal TotalCredit, decimal Balance);
public sealed class CreateFundAccountModel
{
    public long GroupId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Cash";
    public bool IsSystemAccount { get; set; }
}
public sealed record UpdateGroupFundAccountModel(string AccountName, string AccountType, bool IsActive);
public sealed class CreateOpeningFundModel
{
    public long GroupId { get; set; }
    public long FinancialAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}
public sealed class FundTransferModel
{
    public long GroupId { get; set; }
    public long FromFinancialAccountId { get; set; }
    public long ToFinancialAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}
public sealed record GroupFundSummaryModel(long GroupId, DateTime PeriodStart, DateTime PeriodEnd, decimal OpeningFund, decimal MemberContributions, decimal LoanInterest, decimal OtherIncome, decimal InvestmentIncome, decimal TotalAvailableFund, decimal LoansGiven, decimal Expenses, decimal AssetPurchases, decimal Withdrawals, decimal ClosingFund, decimal CashBankBalance, decimal CashBalance, decimal BankBalance);
public sealed record FundTransferResultModel(long FinancialTransactionId, string TransactionNumber, DateTime TransactionDate, long FromFinancialAccountId, long ToFinancialAccountId, decimal Amount, string Status, string? ReferenceNumber, string? Description);
public sealed record OpeningFundResultModel(long FinancialTransactionId, string TransactionNumber, DateTime TransactionDate, long FinancialAccountId, decimal Amount, string Status, string? ReferenceNumber, string? Description);

public sealed class GroupFundApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, IReadOnlyList<GroupFundAccountModel>? Data)> GetAccountsAsync(long groupId) => SendGetListAsync<GroupFundAccountModel>($"api/group-fund/accounts?groupId={groupId}");
    public Task<(bool Success, string? Error, GroupFundAccountModel? Data)> CreateAccountAsync(CreateFundAccountModel model) => SendAsync<CreateFundAccountModel, GroupFundAccountModel>(HttpMethod.Post, "api/financial-accounts", model);
    public Task<(bool Success, string? Error, GroupFundAccountModel? Data)> UpdateAccountAsync(long id, UpdateGroupFundAccountModel model) => SendAsync<UpdateGroupFundAccountModel, GroupFundAccountModel>(HttpMethod.Put, $"api/group-fund/accounts/{id}", model);
    public Task<(bool Success, string? Error, OpeningFundResultModel? Data)> CreateOpeningFundAsync(CreateOpeningFundModel model) => SendAsync<CreateOpeningFundModel, OpeningFundResultModel>(HttpMethod.Post, "api/group-fund/opening-fund", model);
    public Task<(bool Success, string? Error, FundTransferResultModel? Data)> TransferAsync(FundTransferModel model) => SendAsync<FundTransferModel, FundTransferResultModel>(HttpMethod.Post, "api/group-fund/transfers", model);
    public Task<(bool Success, string? Error, GroupFundSummaryModel? Data)> GetSummaryAsync(long groupId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var uri = $"api/group-fund/summary?groupId={groupId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        return SendGetAsync<GroupFundSummaryModel>(uri);
    }

    private async Task<(bool Success, string? Error, IReadOnlyList<T>? Data)> SendGetListAsync<T>(string uri)
    {
        var result = await SendGetAsync<List<T>>(uri);
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
    private sealed record ErrorResponse(string Message);
}
