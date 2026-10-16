using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;
public sealed record NotificationModel(long Id,long? UserId,string NotificationType,string Title,string Message,bool IsRead,DateTime? ReadAtUtc,DateTime CreatedAtUtc);
public sealed record NotificationListModel(IReadOnlyList<NotificationModel> Notifications,int UnreadCount);
public sealed record NotificationEvaluationModel(int CreatedCount,int UpcomingContributions,int OverdueContributions,int UpcomingLoanInstallments,int OverdueLoans,int LoanApprovals,int LoanDisbursements,int PaymentReceipts,int ProfitDistributions);
public sealed class NotificationApiClient(HttpClient http,CustomAuthenticationStateProvider authState)
{
 public async Task<(bool Success,string? Error,NotificationListModel? Data)> GetAsync(bool unreadOnly=false,string? type=null){var u=$"api/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";if(!string.IsNullOrWhiteSpace(type))u+=$"&type={Uri.EscapeDataString(type)}";using var r=Create(HttpMethod.Get,u);using var x=await http.SendAsync(r);if(!x.IsSuccessStatusCode)return(false,await Err(x),null);var d=await x.Content.ReadFromJsonAsync<NotificationListModel>();return d is null?(false,"The API returned an invalid notification response.",null):(true,null,d);}
 public async Task<(bool Success,string? Error)> MarkReadAsync(long id){using var r=Create(HttpMethod.Post,$"api/notifications/{id}/read");using var x=await http.SendAsync(r);return x.IsSuccessStatusCode?(true,null):(false,await Err(x));}
 public async Task<(bool Success,string? Error)> MarkAllReadAsync(){using var r=Create(HttpMethod.Post,"api/notifications/read-all");using var x=await http.SendAsync(r);return x.IsSuccessStatusCode?(true,null):(false,await Err(x));}
 public async Task<(bool Success,string? Error,NotificationEvaluationModel? Data)> EvaluateAsync(long groupId,DateTime? asOfDate=null,int daysAhead=7){var u=$"api/notifications/evaluate?groupId={groupId}&daysAhead={daysAhead}";if(asOfDate.HasValue)u+=$"&asOfDate={Uri.EscapeDataString(asOfDate.Value.ToString("O"))}";using var r=Create(HttpMethod.Post,u);using var x=await http.SendAsync(r);if(!x.IsSuccessStatusCode)return(false,await Err(x),null);var d=await x.Content.ReadFromJsonAsync<NotificationEvaluationModel>();return d is null?(false,"The API returned an invalid notification evaluation response.",null):(true,null,d);}
 private HttpRequestMessage Create(HttpMethod m,string u){var r=new HttpRequestMessage(m,u);if(!string.IsNullOrWhiteSpace(authState.AccessToken))r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",authState.AccessToken);return r;}
 private static async Task<string> Err(HttpResponseMessage x){try{var e=await x.Content.ReadFromJsonAsync<ErrorResponse>();return e?.Message??$"Request failed with HTTP {(int)x.StatusCode}.";}catch{return $"Request failed with HTTP {(int)x.StatusCode}.";}}
 private sealed record ErrorResponse(string Message);
}
