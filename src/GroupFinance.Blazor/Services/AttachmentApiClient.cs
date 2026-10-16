using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record AttachmentModel(
    long Id,
    string EntityName,
    long EntityId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);

public sealed class AttachmentApiClient(
    HttpClient http,
    CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<AttachmentModel>? Data)> ListAsync(
        string entityName,
        long entityId)
    {
        using var request = Create(
            HttpMethod.Get,
            $"api/attachments?entityName={Uri.EscapeDataString(entityName)}&entityId={entityId}");

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<List<AttachmentModel>>();
        return data is null
            ? (false, "The API returned an invalid attachment response.", null)
            : (true, null, data);
    }

    public async Task<(bool Success, string? Error, AttachmentModel? Data)> UploadAsync(
        string entityName,
        long entityId,
        IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(entityName), "entityName");
        content.Add(new StringContent(entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "entityId");

        const long maxFileSizeBytes = 10 * 1024 * 1024;
        if (file.Size <= 0)
            return (false, "The selected file is empty.", null);
        if (file.Size > maxFileSizeBytes)
            return (false, "The selected file exceeds the maximum size of 10 MB.", null);

        // The explicit Components.Web package reference ensures the expected
        // IBrowserFile.OpenReadStream(long) API is resolved consistently.
        var stream = file.OpenReadStream(maxFileSizeBytes + 1);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType);
        content.Add(fileContent, "file", Path.GetFileName(file.Name));

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/attachments/upload")
        {
            Content = content
        };
        AddAuthorization(request);

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), null);

        var data = await response.Content.ReadFromJsonAsync<AttachmentModel>();
        return data is null
            ? (false, "The API returned an invalid attachment response.", null)
            : (true, null, data);
    }

    public async Task<(bool Success, string? Error, byte[]? Data, string? FileName, string? ContentType)> DownloadAsync(long id)
    {
        using var request = Create(HttpMethod.Get, $"api/attachments/{id}/download");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), null, null, null);

        var data = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "attachment";
        return (true, null, data, fileName, contentType);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(long id)
    {
        using var request = Create(HttpMethod.Delete, $"api/attachments/{id}");
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, await ReadErrorAsync(response));
    }

    private HttpRequestMessage Create(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        AddAuthorization(request);
        return request;
    }

    private void AddAuthorization(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
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
