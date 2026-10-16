using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record ReceiptLineModel(
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

public sealed record ReceiptModel(
    long FinancialTransactionId,
    string ReceiptType,
    long GroupId,
    string GroupCode,
    string GroupName,
    long? MemberId,
    string? MemberNumber,
    string? MemberName,
    string TransactionNumber,
    DateTime TransactionDate,
    decimal Amount,
    string? PaymentMethod,
    string? Description,
    string? ReferenceNumber,
    string PreparedBy,
    string Status,
    IReadOnlyList<ReceiptLineModel> Lines);

public sealed class ReceiptApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, ReceiptModel? Data)> GetAsync(long transactionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/receipts/{transactionId}");
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), default);

        var data = await response.Content.ReadFromJsonAsync<ReceiptModel>();
        return data is null
            ? (false, "The API returned an invalid receipt response.", default)
            : (true, null, data);
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
