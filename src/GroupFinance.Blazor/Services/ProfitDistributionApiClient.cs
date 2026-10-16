using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class ProfitDistributionModel
{
    public long GroupId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string DistributionMethod { get; set; } = "ContributionBased";
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
}

public sealed class ProfitDistributionPreviewModel
{
    public long GroupId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string DistributionMethod { get; set; } = "ContributionBased";
}

public sealed record ProfitDistributionMemberModel(
    long MemberId, string MemberNumber, string MemberName,
    decimal ContributionAmount, decimal MembershipMonths, decimal AverageBalance,
    decimal EligibilityFactor, decimal CalculatedShare, decimal DistributedAmount);

public sealed record ProfitDistributionResponseModel(
    long Id, long GroupId, DateTime PeriodStart, DateTime PeriodEnd,
    decimal NetProfit, decimal ReserveAmount, decimal DistributableAmount,
    string DistributionMethod, string Status, DateTime? DistributedDate,
    IReadOnlyCollection<ProfitDistributionMemberModel> Details, long? FinancialTransactionId);

public sealed class ProfitDistributionApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, ProfitDistributionResponseModel? Data)> PreviewAsync(ProfitDistributionPreviewModel model)
        => SendAsync<ProfitDistributionPreviewModel, ProfitDistributionResponseModel>(HttpMethod.Post, "api/profit-distributions/preview", model);

    public Task<(bool Success, string? Error, ProfitDistributionResponseModel? Data)> CreateAsync(ProfitDistributionModel model)
        => SendAsync<ProfitDistributionModel, ProfitDistributionResponseModel>(HttpMethod.Post, "api/profit-distributions", model);

    public async Task<(bool Success, string? Error, IReadOnlyList<ProfitDistributionResponseModel>? Data)> GetAsync(long groupId)
    {
        var result = await SendGetAsync<List<ProfitDistributionResponseModel>>($"api/profit-distributions?groupId={groupId}");
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
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record ErrorResponse(string Message);
}
