using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record InterestCalculationModel(
    long Id, long LoanId, string LoanNumber, long MemberId, string MemberName,
    DateTime CalculationDate, DateTime PeriodStart, DateTime PeriodEnd,
    decimal OpeningPrincipal, decimal InterestRate, decimal InterestAmount,
    string CalculationMethod, bool IsPosted);

public sealed class CalculateInterestModel
{
    public long GroupId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long? LoanId { get; set; }
}

public sealed class CalculateProfitModel
{
    public long GroupId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

public sealed record ProfitCalculationModel(
    long GroupId, DateTime PeriodStart, DateTime PeriodEnd,
    decimal LoanInterestIncome, decimal InvestmentIncome, decimal OtherIncome,
    decimal TotalIncome, decimal Expenses, decimal OtherCosts, decimal TotalCosts,
    decimal NetProfit, decimal ReserveRate, decimal ReserveAmount, decimal DistributableAmount);

public sealed class InterestProfitApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, IReadOnlyList<InterestCalculationModel>? Data)> CalculateInterestAsync(CalculateInterestModel model)
        => SendListAsync<CalculateInterestModel, InterestCalculationModel>(HttpMethod.Post, "api/interest-profit/interest/calculate", model);

    public async Task<(bool Success, string? Error, IReadOnlyList<InterestCalculationModel>? Data)> GetInterestAsync(long groupId, DateTime? fromDate = null, DateTime? toDate = null, long? loanId = null)
    {
        var uri = $"api/interest-profit/interest?groupId={groupId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        if (loanId.HasValue) uri += $"&loanId={loanId.Value}";
        return await SendGetListAsync<InterestCalculationModel>(uri);
    }

    public Task<(bool Success, string? Error, ProfitCalculationModel? Data)> CalculateProfitAsync(CalculateProfitModel model)
        => SendAsync<CalculateProfitModel, ProfitCalculationModel>(HttpMethod.Post, "api/interest-profit/profit/calculate", model);

    private async Task<(bool Success, string? Error, IReadOnlyList<TResponse>? Data)> SendListAsync<TRequest, TResponse>(HttpMethod method, string uri, TRequest model)
    {
        var result = await SendAsync<TRequest, List<TResponse>>(method, uri, model);
        return (result.Success, result.Error, result.Data);
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
