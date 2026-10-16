using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record CreateContributionPlanModel(long GroupId, string Name, decimal Amount, string Frequency, int IntervalMonths, int DueDay, decimal LateFeeRate, bool IsActive = true);
public sealed record UpdateContributionPlanModel(string Name, decimal Amount, string Frequency, int IntervalMonths, int DueDay, decimal LateFeeRate, bool IsActive);
public sealed record ContributionPlanModel(long Id, long GroupId, string GroupCode, string GroupName, string Name, decimal Amount, string Frequency, int IntervalMonths, int DueDay, decimal LateFeeRate, bool IsActive, DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);
public sealed record GenerateContributionSchedulesModel(long ContributionPlanId, DateTime FromDate, DateTime ToDate);
public sealed record ContributionScheduleModel(long Id, long ContributionPlanId, string PlanName, long MemberId, string MemberNumber, string MemberName, DateTime PeriodStart, DateTime PeriodEnd, DateTime DueDate, decimal DueAmount, decimal PaidAmount, decimal LateFeeAmount, decimal WaivedLateFeeAmount, decimal TotalDue, decimal OutstandingAmount, string Status, IReadOnlyCollection<ContributionPaymentModel> Payments);
public sealed record ContributionPaymentModel(long Id, long ContributionScheduleId, long MemberId, DateTime TransactionDate, decimal Amount, string PaymentMethod, long? FinancialAccountId, string? ReferenceNumber, string? Description, string Status, long? FinancialTransactionId, DateTime CreatedAtUtc, long? CreatedByUserId);
public sealed record CreateContributionPaymentModel(DateTime TransactionDate, decimal Amount, string PaymentMethod, long FinancialAccountId, string? ReferenceNumber, string? Description);
public sealed record WaiveContributionLateFeeModel(decimal? Amount, string? Reason);
public sealed record ContributionScheduleGenerationModel(long ContributionPlanId, DateTime FromDate, DateTime ToDate, int SchedulesCreated, int SchedulesAlreadyExisting);
public sealed record FinancialAccountModel(long Id, long GroupId, string AccountCode, string AccountName, string AccountType, bool IsSystemAccount, bool IsActive);

public sealed class ContributionApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<ContributionPlanModel>? Data)> GetPlansAsync(long groupId, bool includeInactive = false)
    {
        var result = await SendGetAsync<List<ContributionPlanModel>>($"api/contributions/plans?groupId={groupId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        return (result.Success, result.Error, result.Data);
    }
    public Task<(bool Success, string? Error, ContributionPlanModel? Data)> GetPlanAsync(long id) => SendGetAsync<ContributionPlanModel>($"api/contributions/plans/{id}");
    public Task<(bool Success, string? Error, ContributionPlanModel? Data)> CreatePlanAsync(CreateContributionPlanModel model) => SendAsync<CreateContributionPlanModel, ContributionPlanModel>(HttpMethod.Post, "api/contributions/plans", model);
    public Task<(bool Success, string? Error, ContributionPlanModel? Data)> UpdatePlanAsync(long id, UpdateContributionPlanModel model) => SendAsync<UpdateContributionPlanModel, ContributionPlanModel>(HttpMethod.Put, $"api/contributions/plans/{id}", model);
    public Task<(bool Success, string? Error, ContributionScheduleGenerationModel? Data)> GenerateSchedulesAsync(GenerateContributionSchedulesModel model) => SendAsync<GenerateContributionSchedulesModel, ContributionScheduleGenerationModel>(HttpMethod.Post, "api/contributions/schedules/generate", model);
    public async Task<(bool Success, string? Error, IReadOnlyList<ContributionScheduleModel>? Data)> GetSchedulesAsync(long groupId, long? memberId = null, long? planId = null, DateTime? fromDate = null, DateTime? toDate = null, string? status = null)
    {
        var uri = $"api/contributions/schedules?groupId={groupId}";
        if (memberId.HasValue) uri += $"&memberId={memberId}";
        if (planId.HasValue) uri += $"&planId={planId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        if (!string.IsNullOrWhiteSpace(status)) uri += $"&status={Uri.EscapeDataString(status)}";
        var result = await SendGetAsync<List<ContributionScheduleModel>>(uri);
        return (result.Success, result.Error, result.Data);
    }
    public Task<(bool Success, string? Error, ContributionScheduleModel? Data)> GetScheduleAsync(long id) => SendGetAsync<ContributionScheduleModel>($"api/contributions/schedules/{id}");
    public Task<(bool Success, string? Error, ContributionPaymentModel? Data)> RecordPaymentAsync(long id, CreateContributionPaymentModel model) => SendAsync<CreateContributionPaymentModel, ContributionPaymentModel>(HttpMethod.Post, $"api/contributions/schedules/{id}/payments", model);
    public Task<(bool Success, string? Error, ContributionScheduleModel? Data)> WaiveLateFeeAsync(long id, WaiveContributionLateFeeModel model) => SendAsync<WaiveContributionLateFeeModel, ContributionScheduleModel>(HttpMethod.Post, $"api/contributions/schedules/{id}/waive-late-fee", model);
    public async Task<(bool Success, string? Error, IReadOnlyList<FinancialAccountModel>? Data)> GetAccountsAsync(long groupId)
    {
        var result = await SendGetAsync<List<FinancialAccountModel>>($"api/financial-accounts?groupId={groupId}");
        return (result.Success, result.Error, result.Data);
    }

    private async Task<(bool Success, string? Error, T? Data)> SendGetAsync<T>(string uri)
    {
        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await http.SendAsync(request);
        return await ReadResponseAsync<T>(response);
    }
    private async Task<(bool Success, string? Error, TResponse? Data)> SendAsync<TRequest, TResponse>(HttpMethod method, string uri, TRequest model)
    {
        using var request = CreateRequest(method, uri);
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        return await ReadResponseAsync<TResponse>(response);
    }
    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        return request;
    }
    private static async Task<(bool Success, string? Error, T? Data)> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
        var data = await response.Content.ReadFromJsonAsync<T>();
        return data is null ? (false, "The API returned an invalid response.", default) : (true, null, data);
    }
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try { var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}."; }
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }
    private sealed record ErrorResponse(string Message);
}
