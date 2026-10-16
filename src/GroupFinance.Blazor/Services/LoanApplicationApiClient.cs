using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class LoanApplicationCreateModel
{
    public long GroupId { get; set; }
    public long MemberId { get; set; }
    public DateTime ApplicationDate { get; set; } = DateTime.Today;
    public decimal RequestedAmount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int? RequestedDurationMonths { get; set; }
    public decimal? ProposedInterestRate { get; set; }
    public string? ProposedInterestMethod { get; set; }
}

public sealed class LoanApplicationUpdateModel
{
    public DateTime ApplicationDate { get; set; } = DateTime.Today;
    public decimal RequestedAmount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int RequestedDurationMonths { get; set; }
    public decimal ProposedInterestRate { get; set; }
    public string ProposedInterestMethod { get; set; } = "ReducingBalance";
}

public sealed class LoanApplicationGuarantorRequestModel
{
    public long MemberId { get; set; }
}

public sealed class LoanApplicationDecisionModel
{
    public string? Remarks { get; set; }
}

public sealed record LoanApplicationGuarantorModel(
    long Id, long LoanApplicationId, long MemberId, string MemberNumber, string MemberName,
    string Status, DateTime ProvidedAt, DateTime CreatedAtUtc, long? CreatedByUserId);

public sealed record LoanEligibilityModel(
    bool IsEligible, int MembershipMonths, decimal TotalContribution, decimal MaximumEligibleAmount,
    int ActiveLoanCount, int MaximumActiveLoans, int MinimumMembershipMonths,
    decimal LoanContributionMultiplier, IReadOnlyList<string> Reasons);

public sealed record LoanApplicationModel(
    long Id, long GroupId, string GroupCode, string GroupName, long MemberId, string MemberNumber,
    string MemberName, string LoanApplicationNumber, DateTime ApplicationDate, decimal RequestedAmount,
    string Purpose, decimal ProposedInterestRate, string ProposedInterestMethod, int RequestedDurationMonths,
    decimal RequestedEmi, string Status, DateTime? DecisionDate, long? DecidedByUserId, string? DecisionRemarks,
    IReadOnlyCollection<LoanApplicationGuarantorModel> Guarantors, LoanEligibilityModel Eligibility,
    DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);

public sealed record LoanApplicationSummaryModel(
    long Id, long GroupId, long MemberId, string MemberNumber, string MemberName,
    string LoanApplicationNumber, DateTime ApplicationDate, decimal RequestedAmount,
    decimal ProposedInterestRate, string ProposedInterestMethod, int RequestedDurationMonths,
    decimal RequestedEmi, string Status, int GuarantorCount, DateTime? DecisionDate);

public sealed class LoanApplicationApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<LoanApplicationSummaryModel>? Applications)> GetApplicationsAsync(long groupId, string? status = null, long? memberId = null)
    {
        var query = $"api/loan-applications?groupId={groupId}";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (memberId.HasValue) query += $"&memberId={memberId.Value}";
        using var request = CreateRequest(HttpMethod.Get, query);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        return (true, null, await response.Content.ReadFromJsonAsync<List<LoanApplicationSummaryModel>>() ?? []);
    }

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> GetAsync(long id)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Get, $"api/loan-applications/{id}");

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> CreateAsync(LoanApplicationCreateModel model)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Post, "api/loan-applications", model);

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> UpdateAsync(long id, LoanApplicationUpdateModel model)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Put, $"api/loan-applications/{id}", model);

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> SubmitAsync(long id)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Post, $"api/loan-applications/{id}/submit", new { });

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> ApproveAsync(long id, string? remarks)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Post, $"api/loan-applications/{id}/approve", new LoanApplicationDecisionModel { Remarks = remarks });

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> RejectAsync(long id, string? remarks)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Post, $"api/loan-applications/{id}/reject", new LoanApplicationDecisionModel { Remarks = remarks });

    public async Task<(bool Success, string? Error, LoanApplicationModel? Data)> CancelAsync(long id)
        => await SendAsync<LoanApplicationModel>(HttpMethod.Post, $"api/loan-applications/{id}/cancel", new { });

    public async Task<(bool Success, string? Error, LoanApplicationGuarantorModel? Data)> AddGuarantorAsync(long id, long memberId)
        => await SendAsync<LoanApplicationGuarantorModel>(HttpMethod.Post, $"api/loan-applications/{id}/guarantors", new LoanApplicationGuarantorRequestModel { MemberId = memberId });

    public async Task<(bool Success, string? Error, bool Deleted)> RemoveGuarantorAsync(long id, long guarantorId)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"api/loan-applications/{id}/guarantors/{guarantorId}");
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null, true) : (false, await ReadErrorAsync(response), false);
    }

    private async Task<(bool Success, string? Error, T? Data)> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = CreateRequest(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
        return (true, null, await response.Content.ReadFromJsonAsync<T>());
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var token = authState.AccessToken;
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try { var error = await response.Content.ReadFromJsonAsync<ApiError>(); return error?.Message ?? $"Request failed ({(int)response.StatusCode})."; }
        catch { return $"Request failed ({(int)response.StatusCode})."; }
    }

    private sealed record ApiError(string? Message);
}
