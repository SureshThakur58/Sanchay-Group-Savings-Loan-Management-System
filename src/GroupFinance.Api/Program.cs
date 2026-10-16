using System.Text;
using GroupFinance.Api.Authentication;
using GroupFinance.Api.Contributions;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Api.GroupFund;
using GroupFinance.Api.Groups;
using GroupFinance.Api.Members;
using GroupFinance.Api.LoanCalculations;
using GroupFinance.Api.LoanApplications;
using GroupFinance.Api.LoanDisbursements;
using GroupFinance.Api.LoanRepayments;
using GroupFinance.Api.OverdueLoans;
using GroupFinance.Api.Expenses;
using GroupFinance.Api.Assets;
using GroupFinance.Api.InterestProfit;
using GroupFinance.Api.ProfitDistributions;
using GroupFinance.Api.Ledger;
using GroupFinance.Api.Dashboard;
using GroupFinance.Api.Reports;
using GroupFinance.Api.Receipts;
using GroupFinance.Api.Audit;
using GroupFinance.Api.Notifications;
using GroupFinance.Api.FileStorage;
using GroupFinance.Api.Administration;
using GroupFinance.Api.Backup;
using GroupFinance.Core.Security;
using GroupFinance.Core.Loans;
using GroupFinance.Infrastructure;
using GroupFinance.Infrastructure.Authentication;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
    throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 bytes.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GroupFinance API", Version = "v1", Description = "Group Savings & Loan Management System API" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Enter: Bearer {JWT token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddGroupFinanceInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FinancialTransactionService>();
builder.Services.AddScoped<GroupFundService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddSingleton<ILoanCalculationService, LoanCalculationService>();
builder.Services.AddScoped<ApiLoanCalculationService>();
builder.Services.AddScoped<LoanApplicationService>();
builder.Services.AddScoped<LoanDisbursementService>();
builder.Services.AddScoped<LoanRepaymentService>();
builder.Services.AddScoped<OverdueLoanService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<InterestProfitService>();
builder.Services.AddScoped<ProfitDistributionService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ReversalService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.Configure<AttachmentOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.AddScoped<BackupService>();
builder.Services.AddHostedService<ScheduledBackupWorker>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionCodes.All)
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddCors(options => options.AddPolicy("BlazorDevelopment", policy =>
    policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
          .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<GroupFinanceDbContext>("database");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, logger);
    var db = scope.ServiceProvider.GetRequiredService<GroupFinanceDbContext>();
    await GroupSchemaInitializer.InitializeAsync(db, logger);
    await MemberSchemaInitializer.InitializeAsync(db, logger);
    await ContributionSchemaInitializer.InitializeAsync(db, logger);
    await FinancialTransactionSchemaInitializer.InitializeAsync(db, logger);
    await LoanApplicationSchemaInitializer.InitializeAsync(db, logger);
    await LoanRepaymentSchemaInitializer.InitializeAsync(db, logger);
    await ExpenseSchemaInitializer.InitializeAsync(db, logger);
    await AssetSchemaInitializer.InitializeAsync(db, logger);
    await AuthSchemaInitializer.InitializeAsync(db, logger);
    await AuthSeeder.SeedAsync(db, app.Configuration, logger);
}

app.UseSerilogRequestLogging();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("BlazorDevelopment");
}
else if (app.Environment.IsStaging())
{
    // Local/staging Blazor WASM is commonly served from a separate localhost port.
    app.UseCors("BlazorDevelopment");
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program
{
}
