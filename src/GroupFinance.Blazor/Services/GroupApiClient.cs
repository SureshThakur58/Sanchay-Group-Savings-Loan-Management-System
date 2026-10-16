using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record CreateGroupModel(
    string Code, string Name, string? Description, DateTime FormationDate, string? RegistrationNumber, string CurrencyCode, GroupSettingsModel Settings);

public sealed record UpdateGroupModel(
    string Code, string Name, string? Description, DateTime FormationDate, string? RegistrationNumber, string CurrencyCode, bool IsActive);

public sealed class GroupSettingsModel
{
    public decimal DefaultContributionAmount { get; set; }
    public string ContributionFrequency { get; set; } = "Quarterly";
    public decimal DefaultLoanInterestRate { get; set; } = 12m;
    public string LoanInterestMethod { get; set; } = "ReducingBalance";
    public int DefaultLoanDuration { get; set; } = 12;
    public string LoanDurationUnit { get; set; } = "Months";
    public string LoanInstallmentFrequency { get; set; } = "Monthly";
    public decimal ContributionLateFeeRate { get; set; } = 1m;
    public decimal ProfitReserveRate { get; set; } = 10m;
    public string ProfitDistributionMethod { get; set; } = "ContributionBased";
    public decimal LoanContributionMultiplier { get; set; } = 3m;
    public int MinimumMembershipMonthsForLoan { get; set; } = 6;
    public int MaximumActiveLoansPerMember { get; set; } = 1;
    public int RequiredGuarantors { get; set; } = 2;
    public int MonetaryPrecision { get; set; } = 2;
    public string DateFormat { get; set; } = "DD/MM/YYYY";
    public bool AllowBackdatedTransactions { get; set; } = true;
    public bool AllowFutureFinancialPosting { get; set; }

    public GroupSettingsModel() { }

    public GroupSettingsModel(decimal defaultContributionAmount, string contributionFrequency, decimal defaultLoanInterestRate, string loanInterestMethod, int defaultLoanDuration, string loanDurationUnit, string loanInstallmentFrequency, decimal contributionLateFeeRate, decimal profitReserveRate, string profitDistributionMethod, decimal loanContributionMultiplier, int minimumMembershipMonthsForLoan, int maximumActiveLoansPerMember, int requiredGuarantors, int monetaryPrecision, string dateFormat, bool allowBackdatedTransactions, bool allowFutureFinancialPosting)
    {
        DefaultContributionAmount = defaultContributionAmount; ContributionFrequency = contributionFrequency; DefaultLoanInterestRate = defaultLoanInterestRate;
        LoanInterestMethod = loanInterestMethod; DefaultLoanDuration = defaultLoanDuration; LoanDurationUnit = loanDurationUnit;
        LoanInstallmentFrequency = loanInstallmentFrequency; ContributionLateFeeRate = contributionLateFeeRate; ProfitReserveRate = profitReserveRate;
        ProfitDistributionMethod = profitDistributionMethod; LoanContributionMultiplier = loanContributionMultiplier;
        MinimumMembershipMonthsForLoan = minimumMembershipMonthsForLoan; MaximumActiveLoansPerMember = maximumActiveLoansPerMember;
        RequiredGuarantors = requiredGuarantors; MonetaryPrecision = monetaryPrecision; DateFormat = dateFormat;
        AllowBackdatedTransactions = allowBackdatedTransactions; AllowFutureFinancialPosting = allowFutureFinancialPosting;
    }
}

public sealed record GroupModel(
    long Id,
    string Code,
    string Name,
    string? Description,
    DateTime FormationDate,
    string? RegistrationNumber,
    string CurrencyCode,
    bool IsActive,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId,
    GroupSettingsResponseModel? Settings);

public sealed record GroupSettingsResponseModel(
    long Id,
    long GroupId,
    decimal DefaultContributionAmount,
    string ContributionFrequency,
    decimal DefaultLoanInterestRate,
    string LoanInterestMethod,
    int DefaultLoanDuration,
    string LoanDurationUnit,
    string LoanInstallmentFrequency,
    decimal ContributionLateFeeRate,
    decimal ProfitReserveRate,
    string ProfitDistributionMethod,
    decimal LoanContributionMultiplier,
    int MinimumMembershipMonthsForLoan,
    int MaximumActiveLoansPerMember,
    int RequiredGuarantors,
    int MonetaryPrecision,
    string DateFormat,
    bool AllowBackdatedTransactions,
    bool AllowFutureFinancialPosting,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed class GroupApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<GroupModel>? Groups)> GetGroupsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/groups");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var groups = await response.Content.ReadFromJsonAsync<List<GroupModel>>();
        return (true, null, groups ?? []);
    }

    public async Task<(bool Success, string? Error, GroupModel? Group)> CreateAsync(CreateGroupModel requestBody)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/groups");
        request.Content = JsonContent.Create(requestBody);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var group = await response.Content.ReadFromJsonAsync<GroupModel>();
        return group is null ? (false, "The API returned an invalid group response.", null) : (true, null, group);
    }

    public async Task<(bool Success, string? Error, GroupModel? Group)> UpdateAsync(long id, UpdateGroupModel requestBody)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/groups/{id}");
        request.Content = JsonContent.Create(requestBody);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var group = await response.Content.ReadFromJsonAsync<GroupModel>();
        return group is null ? (false, "The API returned an invalid group response.", null) : (true, null, group);
    }

    public async Task<(bool Success, string? Error, GroupSettingsResponseModel? Settings)> UpdateSettingsAsync(long id, GroupSettingsModel model)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/groups/{id}/settings");
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var settings = await response.Content.ReadFromJsonAsync<GroupSettingsResponseModel>();
        return settings is null ? (false, "The API returned an invalid group settings response.", null) : (true, null, settings);
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
