using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record AuditLogModel(
    long Id,
    long? UserId,
    string UserName,
    string EntityName,
    long? EntityId,
    string Action,
    string? OldValuesJson,
    string? NewValuesJson,
    string? Reason,
    string? IpAddress,
    DateTime CreatedAtUtc);

public sealed record ReversalResultModel(
    long Id,
    string TransactionNumber,
    DateTime TransactionDate,
    long GroupId,
    long? MemberId,
    string TransactionType,
    string Status,
    string? Description,
    string? ReferenceType,
    long? ReferenceId,
    string? ReferenceNumber,
    decimal Amount,
    decimal TotalDebit,
    decimal TotalCredit,
    long? CreatedByUserId,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<FinancialTransactionLineModel> Lines);

public sealed record FinancialTransactionLineModel(
    long Id, long FinancialAccountId, string AccountCode, string AccountName,
    decimal DebitAmount, decimal CreditAmount, string? Description);

public sealed record ReverseTransactionRequestModel(
    DateTime? ReversalDate,
    string? Reason,
    string? ReferenceNumber);

public sealed class AuditApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<AuditLogModel>? Data)> GetLogsAsync(
        long groupId,
        string? entityName = null,
        long? entityId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = $"api/audit-logs?groupId={groupId}";
        if (!string.IsNullOrWhiteSpace(entityName)) query += $"&entityName={Uri.EscapeDataString(entityName)}";
        if (entityId.HasValue) query += $"&entityId={entityId.Value}";
        if (!string.IsNullOrWhiteSpace(action)) query += $"&action={Uri.EscapeDataString(action)}";
        if (fromDate.HasValue) query += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) query += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";

        using var request = CreateRequest(HttpMethod.Get, query);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<List<AuditLogModel>>();
        return (true, null, data ?? []);
    }

    public async Task<(bool Success, string? Error, ReversalResultModel? Data)> ReverseAsync(
        long transactionId,
        ReverseTransactionRequestModel requestModel)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/reversals/{transactionId}");
        request.Content = JsonContent.Create(requestModel);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<ReversalResultModel>();
        return data is null
            ? (false, "The API returned an invalid reversal response.", null)
            : (true, null, data);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        return request;
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
