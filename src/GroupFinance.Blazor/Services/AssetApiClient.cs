using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record AssetModel(
    long Id, long GroupId, string GroupCode, string GroupName, string AssetCode, string AssetName,
    string Category, DateTime AcquisitionDate, decimal AcquisitionCost, decimal BookValue,
    string? Location, string? Description, bool IsActive, string Status,
    DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);

public sealed record AssetTransactionModel(
    long Id, long AssetId, string AssetCode, string AssetName, DateTime TransactionDate,
    string TransactionType, decimal Amount, decimal? BookValueAtSale, decimal? GainLoss,
    string? Description, long? FinancialTransactionId, string? FinancialTransactionNumber,
    string Status, DateTime CreatedAtUtc, long? CreatedByUserId);

public sealed class CreateAssetModel
{
    public long GroupId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public DateTime AcquisitionDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public long FinancialAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
}

public sealed class SellAssetModel
{
    public DateTime SaleDate { get; set; }
    public decimal SaleAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public long FinancialAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public sealed record AssetFinancialAccountModel(long Id, long GroupId, string AccountCode, string AccountName, string AccountType, bool IsSystemAccount, bool IsActive);

public sealed class AssetApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<AssetModel>? Data)> GetAssetsAsync(long groupId, bool includeInactive = true, string? category = null)
    {
        var uri = $"api/assets?groupId={groupId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(category)) uri += $"&category={Uri.EscapeDataString(category)}";
        return await SendGetListAsync<AssetModel>(uri);
    }

    public Task<(bool Success, string? Error, AssetModel? Data)> GetAssetAsync(long id)
        => SendGetAsync<AssetModel>($"api/assets/{id}");

    public Task<(bool Success, string? Error, AssetModel? Data)> CreateAssetAsync(CreateAssetModel model)
        => SendAsync<CreateAssetModel, AssetModel>(HttpMethod.Post, "api/assets", model);

    public Task<(bool Success, string? Error, AssetTransactionModel? Data)> SellAssetAsync(long id, SellAssetModel model)
        => SendAsync<SellAssetModel, AssetTransactionModel>(HttpMethod.Post, $"api/assets/{id}/sell", model);

    public Task<(bool Success, string? Error, IReadOnlyList<AssetTransactionModel>? Data)> GetTransactionsAsync(long id)
        => SendGetListAsync<AssetTransactionModel>($"api/assets/{id}/transactions");

    public async Task<(bool Success, string? Error, IReadOnlyList<AssetFinancialAccountModel>? Data)> GetAccountsAsync(long groupId)
        => await SendGetListAsync<AssetFinancialAccountModel>($"api/financial-accounts?groupId={groupId}");

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
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        return request;
    }

    private static async Task<(bool Success, string? Error, T? Data)> ReadResponseAsync<T>(HttpResponseMessage response)
    {
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
