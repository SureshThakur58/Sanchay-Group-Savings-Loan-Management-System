using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record CreateMemberModel(
    long GroupId,
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes,
    IReadOnlyCollection<CreateMemberNomineeModel>? Nominees);

public sealed record UpdateMemberModel(
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    DateTime? ExitDate,
    bool IsActive,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes);

public sealed record CreateMemberNomineeModel(
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive = true);

public sealed record UpdateMemberNomineeModel(
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive);

public sealed record MemberNomineeModel(
    long Id,
    long MemberId,
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record MemberModel(
    long Id,
    long GroupId,
    string GroupCode,
    string GroupName,
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    DateTime? ExitDate,
    bool IsActive,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes,
    IReadOnlyCollection<MemberNomineeModel> Nominees,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed class MemberApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<MemberModel>? Members)> GetMembersAsync(long groupId, bool includeInactive)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/members?groupId={groupId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var members = await response.Content.ReadFromJsonAsync<List<MemberModel>>();
        return (true, null, members ?? []);
    }

    public async Task<(bool Success, string? Error, string? MemberNumber)> GetNextMemberNumberAsync(long groupId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/members/next-number?groupId={groupId}");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var payload = await response.Content.ReadFromJsonAsync<NextMemberNumberResponse>();
        return payload?.MemberNumber is null
            ? (false, "The API returned an invalid next member number response.", null)
            : (true, null, payload.MemberNumber);
    }

    public async Task<(bool Success, string? Error, MemberModel? Member)> GetAsync(long id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/members/{id}");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var member = await response.Content.ReadFromJsonAsync<MemberModel>();
        return member is null ? (false, "The API returned an invalid member response.", null) : (true, null, member);
    }

    public async Task<(bool Success, string? Error, MemberModel? Member)> CreateAsync(CreateMemberModel model)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/members");
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var member = await response.Content.ReadFromJsonAsync<MemberModel>();
        return member is null ? (false, "The API returned an invalid member response.", null) : (true, null, member);
    }

    public async Task<(bool Success, string? Error, MemberModel? Member)> UpdateAsync(long id, UpdateMemberModel model)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/members/{id}");
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var member = await response.Content.ReadFromJsonAsync<MemberModel>();
        return member is null ? (false, "The API returned an invalid member response.", null) : (true, null, member);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<MemberNomineeModel>? Nominees)> GetNomineesAsync(long memberId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/members/{memberId}/nominees");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var nominees = await response.Content.ReadFromJsonAsync<List<MemberNomineeModel>>();
        return (true, null, nominees ?? []);
    }

    public async Task<(bool Success, string? Error, MemberNomineeModel? Nominee)> AddNomineeAsync(long memberId, CreateMemberNomineeModel model)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/members/{memberId}/nominees");
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var nominee = await response.Content.ReadFromJsonAsync<MemberNomineeModel>();
        return nominee is null ? (false, "The API returned an invalid nominee response.", null) : (true, null, nominee);
    }

    public async Task<(bool Success, string? Error, MemberNomineeModel? Nominee)> UpdateNomineeAsync(long memberId, long nomineeId, UpdateMemberNomineeModel model)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/members/{memberId}/nominees/{nomineeId}");
        request.Content = JsonContent.Create(model);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), null);
        var nominee = await response.Content.ReadFromJsonAsync<MemberNomineeModel>();
        return nominee is null ? (false, "The API returned an invalid nominee response.", null) : (true, null, nominee);
    }

    public async Task<(bool Success, string? Error)> RemoveNomineeAsync(long memberId, long nomineeId)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"api/members/{memberId}/nominees/{nomineeId}");
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
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
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record ErrorResponse(string Message);
}

public sealed record NextMemberNumberResponse(string MemberNumber);
