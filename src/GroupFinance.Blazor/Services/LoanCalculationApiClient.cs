using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class LoanCalculationRequestModel
{
    public long GroupId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int DurationMonths { get; set; }
    public string InterestMethod { get; set; } = "ReducingBalance";
    public DateTime FirstDueDate { get; set; } = DateTime.Today;
}

public sealed record LoanScheduleLineModel(
    int InstallmentNumber,
    DateTime DueDate,
    decimal OpeningPrincipal,
    decimal PrincipalDue,
    decimal InterestDue,
    decimal TotalDue,
    decimal ClosingPrincipal);

public sealed record LoanCalculationResultModel(
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    string InterestMethod,
    decimal MonthlyInstallment,
    decimal TotalInterest,
    decimal TotalRepayment,
    IReadOnlyCollection<LoanScheduleLineModel> Schedule);

public sealed class LoanCalculationApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, LoanCalculationResultModel? Data)> CalculateAsync(LoanCalculationRequestModel model)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/loan-calculations/preview")
        {
            Content = JsonContent.Create(model)
        };
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<LoanCalculationResultModel>();
        return data is null ? (false, "The API returned an invalid loan calculation response.", null) : (true, null, data);
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
