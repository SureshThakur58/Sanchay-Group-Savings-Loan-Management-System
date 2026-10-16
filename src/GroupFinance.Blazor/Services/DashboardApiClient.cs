using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record DashboardFundDistributionModel(
    decimal Cash,
    decimal Loans,
    decimal Assets,
    decimal Investments);

public sealed record DashboardMonthlyCollectionModel(
    int Month,
    string MonthName,
    decimal Amount);

public sealed record DashboardLoanStatusModel(
    string Status,
    int Count);

public sealed record DashboardIncomeExpenseModel(
    int Month,
    string MonthName,
    decimal Income,
    decimal Expense,
    decimal Profit);

public sealed record DashboardModel(
    long GroupId,
    DateTime AsOfDate,
    string CurrencyCode,
    int TotalMembers,
    int ActiveMembers,
    decimal TotalContributions,
    decimal TotalLoans,
    decimal OutstandingLoans,
    decimal InterestEarned,
    decimal Expenses,
    decimal NetProfit,
    decimal AvailableCash,
    int OverdueLoans,
    decimal OverdueAmount,
    DashboardFundDistributionModel FundDistribution,
    IReadOnlyList<DashboardMonthlyCollectionModel> MonthlyCollections,
    IReadOnlyList<DashboardLoanStatusModel> LoanStatus,
    IReadOnlyList<DashboardIncomeExpenseModel> IncomeVsExpense);

public sealed class DashboardApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, DashboardModel? Data)> GetAsync(
        long groupId,
        DateTime? asOfDate = null)
    {
        var uri = $"api/dashboard?groupId={groupId}";
        if (asOfDate.HasValue)
            uri += $"&asOfDate={Uri.EscapeDataString(asOfDate.Value.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<DashboardModel>();
        return data is null
            ? (false, "The API returned an invalid dashboard response.", null)
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
