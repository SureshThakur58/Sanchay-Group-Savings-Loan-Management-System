using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record LedgerEntryModel(
    long TransactionDetailId,
    long FinancialTransactionId,
    string TransactionNumber,
    DateTime TransactionDate,
    string TransactionType,
    string? Description,
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance,
    long? MemberId,
    string? MemberNumber,
    string? MemberName,
    string? ReferenceNumber);

public sealed record TrialBalanceRowModel(
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record TrialBalanceModel(
    long GroupId,
    DateTime AsOfDate,
    IReadOnlyList<TrialBalanceRowModel> Accounts,
    decimal TotalDebit,
    decimal TotalCredit);

public sealed class LedgerApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<LedgerEntryModel>? Data)> GetGroupLedgerAsync(
        long groupId, DateTime? fromDate, DateTime? toDate, long? accountId)
    {
        var uri = $"api/ledger/group?groupId={groupId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        if (accountId.HasValue) uri += $"&financialAccountId={accountId.Value}";
        return await GetListAsync<LedgerEntryModel>(uri);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<LedgerEntryModel>? Data)> GetMemberLedgerAsync(
        long groupId, long memberId, DateTime? fromDate, DateTime? toDate)
    {
        var uri = $"api/ledger/member?groupId={groupId}&memberId={memberId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        return await GetListAsync<LedgerEntryModel>(uri);
    }

    public async Task<(bool Success, string? Error, TrialBalanceModel? Data)> GetTrialBalanceAsync(long groupId, DateTime asOfDate)
    {
        return await GetAsync<TrialBalanceModel>($"api/ledger/trial-balance?groupId={groupId}&asOfDate={Uri.EscapeDataString(asOfDate.ToString("O"))}");
    }

    private async Task<(bool Success, string? Error, IReadOnlyList<T>? Data)> GetListAsync<T>(string uri)
    {
        var result = await GetAsync<List<T>>(uri);
        return (result.Success, result.Error, result.Data);
    }

    private async Task<(bool Success, string? Error, T? Data)> GetAsync<T>(string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), default);
        var data = await response.Content.ReadFromJsonAsync<T>();
        return data is null ? (false, "The API returned an invalid response.", default) : (true, null, data);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}.";
        }
        catch
        {
            return $"Request failed with HTTP {(int)response.StatusCode}.";
        }
    }

    private sealed record ErrorResponse(string Message);
}
