using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record ExpenseCategoryModel(long Id, long GroupId, string GroupCode, string GroupName, string Name, bool IsActive, DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);
public sealed record CreateExpenseCategoryModel(long GroupId, string Name, bool IsActive = true);
public sealed record UpdateExpenseCategoryModel(string Name, bool IsActive);
public sealed record CreateExpenseModel(long GroupId, long ExpenseCategoryId, DateTime ExpenseDate, decimal Amount, string PaymentMethod, long FinancialAccountId, string? ReferenceNumber, string Description);
public sealed record ExpenseModel(long Id, string ExpenseNumber, long GroupId, string GroupCode, string GroupName, long ExpenseCategoryId, string ExpenseCategoryName, DateTime ExpenseDate, decimal Amount, string PaymentMethod, long FinancialAccountId, string FinancialAccountCode, string FinancialAccountName, string? ReferenceNumber, string Description, string Status, long? FinancialTransactionId, string? FinancialTransactionNumber, DateTime CreatedAtUtc, long? CreatedByUserId);
public sealed record ExpenseFinancialAccountModel(long Id, long GroupId, string AccountCode, string AccountName, string AccountType, bool IsSystemAccount, bool IsActive);

public sealed class ExpenseApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<ExpenseCategoryModel>? Data)> GetCategoriesAsync(long groupId, bool includeInactive = false)
    {
        var result = await SendGetAsync<List<ExpenseCategoryModel>>($"api/expenses/categories?groupId={groupId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        return (result.Success, result.Error, result.Data);
    }
    public Task<(bool Success, string? Error, ExpenseCategoryModel? Data)> CreateCategoryAsync(CreateExpenseCategoryModel model) => SendAsync<CreateExpenseCategoryModel, ExpenseCategoryModel>(HttpMethod.Post, "api/expenses/categories", model);
    public Task<(bool Success, string? Error, ExpenseCategoryModel? Data)> UpdateCategoryAsync(long id, UpdateExpenseCategoryModel model) => SendAsync<UpdateExpenseCategoryModel, ExpenseCategoryModel>(HttpMethod.Put, $"api/expenses/categories/{id}", model);
    public async Task<(bool Success, string? Error, IReadOnlyList<ExpenseModel>? Data)> GetExpensesAsync(long groupId, DateTime? fromDate = null, DateTime? toDate = null, long? categoryId = null, string? status = null)
    {
        var uri = $"api/expenses?groupId={groupId}";
        if (fromDate.HasValue) uri += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) uri += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        if (categoryId.HasValue) uri += $"&categoryId={categoryId.Value}";
        if (!string.IsNullOrWhiteSpace(status)) uri += $"&status={Uri.EscapeDataString(status)}";
        var result = await SendGetAsync<List<ExpenseModel>>(uri);
        return (result.Success, result.Error, result.Data);
    }
    public Task<(bool Success, string? Error, ExpenseModel? Data)> GetExpenseAsync(long id) => SendGetAsync<ExpenseModel>($"api/expenses/{id}");
    public Task<(bool Success, string? Error, ExpenseModel? Data)> CreateExpenseAsync(CreateExpenseModel model) => SendAsync<CreateExpenseModel, ExpenseModel>(HttpMethod.Post, "api/expenses", model);
    public async Task<(bool Success, string? Error, IReadOnlyList<ExpenseFinancialAccountModel>? Data)> GetAccountsAsync(long groupId)
    {
        var result = await SendGetAsync<List<ExpenseFinancialAccountModel>>($"api/financial-accounts?groupId={groupId}");
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
