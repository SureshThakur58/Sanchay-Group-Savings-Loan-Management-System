using GroupFinance.Blazor;
using GroupFinance.Blazor.Authentication;
using GroupFinance.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("Users.View", policy =>
        policy.RequireClaim("permission", "Users.View"));
});
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<GroupApiClient>();
builder.Services.AddScoped<MemberApiClient>();
builder.Services.AddScoped<ContributionApiClient>();
builder.Services.AddScoped<LoanCalculationApiClient>();
builder.Services.AddScoped<LoanApplicationApiClient>();
builder.Services.AddScoped<LoanDisbursementApiClient>();
builder.Services.AddScoped<LoanRepaymentApiClient>();
builder.Services.AddScoped<OverdueLoanApiClient>();
builder.Services.AddScoped<GroupFundApiClient>();
builder.Services.AddScoped<ExpenseApiClient>();
builder.Services.AddScoped<AssetApiClient>();
builder.Services.AddScoped<InterestProfitApiClient>();
builder.Services.AddScoped<ProfitDistributionApiClient>();
builder.Services.AddScoped<LedgerApiClient>();
builder.Services.AddScoped<DashboardApiClient>();
builder.Services.AddScoped<ApiHealthService>();
builder.Services.AddScoped<ReportsApiClient>();
builder.Services.AddScoped<ReceiptApiClient>();
builder.Services.AddScoped<AuditApiClient>();
builder.Services.AddScoped<NotificationApiClient>();
builder.Services.AddScoped<AttachmentApiClient>();
builder.Services.AddScoped<AdministrationApiClient>();

await builder.Build().RunAsync();
