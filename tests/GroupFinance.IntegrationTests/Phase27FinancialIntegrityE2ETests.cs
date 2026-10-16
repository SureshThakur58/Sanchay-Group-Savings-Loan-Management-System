using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.IntegrationTests;

/// <summary>
/// Phase 27 database-backed financial integrity and end-to-end workflow.
/// Run this against a disposable, empty GroupFinance_Phase27_Test database.
/// </summary>
public sealed class Phase27FinancialIntegrityE2ETests : IClassFixture<Phase27WebApplicationFactory>
{
    private readonly Phase27WebApplicationFactory _factory;

    public Phase27FinancialIntegrityE2ETests(Phase27WebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Complete_Financial_Workflow_Reconciles_End_To_End()
    {
        // The Phase 27 E2E database is disposable. Reset it before each full workflow
        // run so a previous failed execution cannot contaminate the single-group test.
        await ResetTestDatabaseAsync();

        using var client = await CreateAuthenticatedClientAsync();
        var today = DateTime.UtcNow.Date;

        // The product is intentionally single-group. A clean disposable database
        // is therefore required for this full lifecycle test.
        var existingGroups = await GetAsync<JsonElement[]>(client, "/api/groups");
        Assert.Empty(existingGroups);

        var groupCode = "P27-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var group = await PostAsync<JsonElement>(client, "/api/groups", new
        {
            code = groupCode,
            name = "Phase 27 Test Group",
            description = "Disposable Phase 27 financial integrity test group",
            formationDate = today.AddMonths(-24),
            registrationNumber = (string?)null,
            currencyCode = "NPR",
            settings = new
            {
                defaultContributionAmount = 100m,
                contributionFrequency = "Monthly",
                defaultLoanInterestRate = 0m,
                loanInterestMethod = "ReducingBalance",
                defaultLoanDuration = 2,
                loanDurationUnit = "Months",
                loanInstallmentFrequency = "Monthly",
                contributionLateFeeRate = 0m,
                profitReserveRate = 10m,
                profitDistributionMethod = "Equal",
                loanContributionMultiplier = 1m,
                minimumMembershipMonthsForLoan = 0,
                maximumActiveLoansPerMember = 1,
                requiredGuarantors = 0,
                monetaryPrecision = 2,
                dateFormat = "yyyy-MM-dd",
                allowBackdatedTransactions = true,
                allowFutureFinancialPosting = false
            }
        });

        var groupId = group.GetProperty("id").GetInt64();
        Assert.True(groupId > 0);

        var member = await PostAsync<JsonElement>(client, "/api/members", new
        {
            groupId,
            memberNumber = "P27-001",
            fullName = "Phase 27 Member",
            address = "Kathmandu",
            mobileNumber = "9800000000",
            email = "phase27@example.test",
            dateOfBirth = (DateTime?)null,
            joinDate = today.AddMonths(-24),
            citizenshipNumber = (string?)null,
            contributionAmountOverride = (decimal?)null,
            notes = "Phase 27 E2E",
            nominees = Array.Empty<object>()
        });
        var memberId = member.GetProperty("id").GetInt64();

        var cash = await PostAsync<JsonElement>(client, "/api/financial-accounts", new
        {
            groupId,
            accountCode = "P27-CASH",
            accountName = "Phase 27 Cash",
            accountType = "Cash",
            isSystemAccount = false
        });
        var cashAccountId = cash.GetProperty("id").GetInt64();

        var plan = await PostAsync<JsonElement>(client, "/api/contributions/plans", new
        {
            groupId,
            name = "Phase 27 Monthly",
            amount = 100m,
            frequency = "Monthly",
            intervalMonths = 1,
            dueDay = today.Day,
            lateFeeRate = 0m,
            isActive = true
        });
        var planId = plan.GetProperty("id").GetInt64();

        var generated = await PostAsync<JsonElement>(client, "/api/contributions/schedules/generate", new
        {
            contributionPlanId = planId,
            fromDate = today,
            toDate = today
        });
        Assert.True(generated.GetProperty("schedulesCreated").GetInt32() >= 1);

        var schedules = await GetAsync<JsonElement[]>(client,
            $"/api/contributions/schedules?groupId={groupId}&memberId={memberId}&planId={planId}");
        var schedule = Assert.Single(schedules);
        var scheduleId = schedule.GetProperty("id").GetInt64();
        Assert.Equal(100m, schedule.GetProperty("dueAmount").GetDecimal());

        var contribution = await PostAsync<JsonElement>(client,
            $"/api/contributions/schedules/{scheduleId}/payments", new
            {
                transactionDate = today,
                amount = 100m,
                paymentMethod = "Cash",
                financialAccountId = cashAccountId,
                referenceNumber = "P27-CONTRIB-001",
                description = "Phase 27 contribution"
            });
        Assert.Equal(100m, contribution.GetProperty("amount").GetDecimal());

        var paidSchedule = await GetAsync<JsonElement>(client, $"/api/contributions/schedules/{scheduleId}");
        Assert.Equal(0m, paidSchedule.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal("Paid", paidSchedule.GetProperty("status").GetString());

        // A retry/double-click with the same external reference must not create a second payment.
        var duplicate = await client.PostAsJsonAsync(
            $"/api/contributions/schedules/{scheduleId}/payments", new
            {
                transactionDate = today,
                amount = 100m,
                paymentMethod = "Cash",
                financialAccountId = cashAccountId,
                referenceNumber = "P27-CONTRIB-001",
                description = "Duplicate Phase 27 contribution"
            });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var application = await PostAsync<JsonElement>(client, "/api/loan-applications", new
        {
            groupId,
            memberId,
            applicationDate = today,
            requestedAmount = 100m,
            purpose = "Phase 27 loan lifecycle",
            requestedDurationMonths = 2,
            proposedInterestRate = 0m,
            proposedInterestMethod = "ReducingBalance"
        });
        var applicationId = application.GetProperty("id").GetInt64();

        var submitted = await PostAsync<JsonElement>(client,
            $"/api/loan-applications/{applicationId}/submit", new { });
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());

        var approved = await PostAsync<JsonElement>(client,
            $"/api/loan-applications/{applicationId}/approve", new { remarks = "Phase 27 approval" });
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        var disbursement = await PostAsync<JsonElement>(client,
            $"/api/loan-disbursements/{applicationId}", new
            {
                financialAccountId = cashAccountId,
                disbursementDate = today,
                referenceNumber = "P27-LOAN-001",
                description = "Phase 27 loan disbursement"
            });
        var loanId = disbursement.GetProperty("loanId").GetInt64();
        Assert.Equal(100m, disbursement.GetProperty("principalAmount").GetDecimal());
        Assert.Equal(0m, disbursement.GetProperty("totalInterest").GetDecimal());

        var cashAfterDisbursement = await GetAsync<JsonElement>(client, $"/api/financial-accounts/{cashAccountId}/balance");
        Assert.Equal(0m, cashAfterDisbursement.GetProperty("balance").GetDecimal());

        var repayment = await PostAsync<JsonElement>(client,
            $"/api/loan-repayments/{loanId}", new
            {
                financialAccountId = cashAccountId,
                transactionDate = today,
                amount = 50m,
                paymentMethod = "Cash",
                referenceNumber = "P27-REPAY-001",
                description = "Phase 27 partial repayment"
            });
        Assert.Equal(50m, repayment.GetProperty("principalAllocated").GetDecimal());
        Assert.Equal(0m, repayment.GetProperty("interestAllocated").GetDecimal());
        Assert.Equal(0m, repayment.GetProperty("penaltyAllocated").GetDecimal());
        Assert.Equal(50m, repayment.GetProperty("outstandingPrincipal").GetDecimal());

        var loan = await GetAsync<JsonElement>(client, $"/api/loan-disbursements/loans/{loanId}");
        Assert.Equal(100m, loan.GetProperty("principalAmount").GetDecimal());
        Assert.Equal(50m, loan.GetProperty("principalPaid").GetDecimal());
        Assert.Equal(50m, loan.GetProperty("outstandingPrincipal").GetDecimal());

        var cashAfterRepayment = await GetAsync<JsonElement>(client, $"/api/financial-accounts/{cashAccountId}/balance");
        Assert.Equal(50m, cashAfterRepayment.GetProperty("balance").GetDecimal());

        // Add explicit income and expense entries so the profit engine is verified through SQL Server.
        var incomeAccount = await PostAsync<JsonElement>(client, "/api/financial-accounts", new
        {
            groupId, accountCode = "P27-INCOME", accountName = "Phase 27 Other Income", accountType = "Income", isSystemAccount = false
        });
        var incomeAccountId = incomeAccount.GetProperty("id").GetInt64();
        var expenseAccount = await PostAsync<JsonElement>(client, "/api/financial-accounts", new
        {
            groupId, accountCode = "P27-EXPENSE", accountName = "Phase 27 Expense", accountType = "Expense", isSystemAccount = false
        });
        var expenseAccountId = expenseAccount.GetProperty("id").GetInt64();

        var incomeTransaction = await PostAsync<JsonElement>(client, "/api/financial-transactions", new
        {
            groupId, memberId = (long?)null, transactionDate = today, transactionType = "OTHER_INCOME",
            description = "Phase 27 test income", referenceType = "Phase27", referenceId = (long?)null, referenceNumber = "P27-INCOME-001",
            lines = new[]
            {
                new { financialAccountId = cashAccountId, debitAmount = 20m, creditAmount = 0m, description = "Cash received" },
                new { financialAccountId = incomeAccountId, debitAmount = 0m, creditAmount = 20m, description = "Other income" }
            }
        });
        var incomeTransactionId = incomeTransaction.GetProperty("id").GetInt64();
        Assert.Equal(incomeTransaction.GetProperty("totalDebit").GetDecimal(), incomeTransaction.GetProperty("totalCredit").GetDecimal());

        var expenseTransaction = await PostAsync<JsonElement>(client, "/api/financial-transactions", new
        {
            groupId, memberId = (long?)null, transactionDate = today, transactionType = "EXPENSE",
            description = "Phase 27 test expense", referenceType = "Phase27", referenceId = (long?)null, referenceNumber = "P27-EXPENSE-001",
            lines = new[]
            {
                new { financialAccountId = expenseAccountId, debitAmount = 5m, creditAmount = 0m, description = "Expense" },
                new { financialAccountId = cashAccountId, debitAmount = 0m, creditAmount = 5m, description = "Cash paid" }
            }
        });
        Assert.Equal(5m, expenseTransaction.GetProperty("totalDebit").GetDecimal());
        Assert.Equal(5m, expenseTransaction.GetProperty("totalCredit").GetDecimal());

        var profit = await PostAsync<JsonElement>(client, "/api/interest-profit/profit/calculate", new
        { groupId, periodStart = today, periodEnd = today });
        Assert.Equal(20m, profit.GetProperty("totalIncome").GetDecimal());
        Assert.Equal(5m, profit.GetProperty("totalCosts").GetDecimal());
        Assert.Equal(15m, profit.GetProperty("netProfit").GetDecimal());
        Assert.Equal(1.5m, profit.GetProperty("reserveAmount").GetDecimal());
        Assert.Equal(13.5m, profit.GetProperty("distributableAmount").GetDecimal());

        var preview = await PostAsync<JsonElement>(client, "/api/profit-distributions/preview", new
        { groupId, periodStart = today, periodEnd = today, distributionMethod = "Equal" });
        Assert.Equal(15m, preview.GetProperty("netProfit").GetDecimal());
        Assert.Equal(1.5m, preview.GetProperty("reserveAmount").GetDecimal());
        Assert.Equal(13.5m, preview.GetProperty("distributableAmount").GetDecimal());

        var distribution = await PostAsync<JsonElement>(client, "/api/profit-distributions", new
        { groupId, periodStart = today, periodEnd = today, distributionMethod = "Equal", description = "Phase 27 profit distribution", referenceNumber = "P27-PROFIT-001" });
        Assert.Equal("Distributed", distribution.GetProperty("status").GetString());
        Assert.Equal(13.5m, distribution.GetProperty("distributableAmount").GetDecimal());
        Assert.Equal(13.5m, distribution.GetProperty("details")[0].GetProperty("distributedAmount").GetDecimal());

        var receipt = await GetAsync<JsonElement>(client, $"/api/receipts/{contribution.GetProperty("financialTransactionId").GetInt64()}");
        Assert.Equal(contribution.GetProperty("financialTransactionId").GetInt64(), receipt.GetProperty("financialTransactionId").GetInt64());
        Assert.Equal(100m, receipt.GetProperty("amount").GetDecimal());

        var audit = await GetAsync<JsonElement[]>(client, $"/api/audit-logs?groupId={groupId}");
        Assert.NotEmpty(audit);

        // Reverse only the standalone test income journal. The original record must be preserved
        // as Reversed and the counter-journal must be balanced and linked to it.
        var reversal = await PostAsync<JsonElement>(client, $"/api/reversals/{incomeTransactionId}", new
        { reversalDate = today, reason = "Phase 27 reversal integrity test", referenceNumber = "P27-REV-001" });
        Assert.Equal(incomeTransactionId, reversal.GetProperty("referenceId").GetInt64());
        Assert.Equal("ADJUSTMENT", reversal.GetProperty("transactionType").GetString());
        Assert.Equal(reversal.GetProperty("totalDebit").GetDecimal(), reversal.GetProperty("totalCredit").GetDecimal());

        var originalAfterReversal = await GetAsync<JsonElement>(client, $"/api/financial-transactions/{incomeTransactionId}");
        Assert.Equal("Reversed", originalAfterReversal.GetProperty("status").GetString());

        var transactions = await GetAsync<JsonElement[]>(client, $"/api/financial-transactions?groupId={groupId}");
        Assert.True(transactions.Length >= 3);
        foreach (var transaction in transactions)
        {
            Assert.Equal(transaction.GetProperty("totalDebit").GetDecimal(), transaction.GetProperty("totalCredit").GetDecimal());
            var status = transaction.GetProperty("status").GetString();
            Assert.Contains(status, new[] { "Posted", "Reversed" });
        }

        var reversalAudit = await GetAsync<JsonElement[]>(client, $"/api/audit-logs?groupId={groupId}&entityName=FinancialTransaction&entityId={incomeTransactionId}&action=Reversed");
        Assert.NotEmpty(reversalAudit);
    }

    private static async Task ResetTestDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("GROUPFINANCE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("GROUPFINANCE_TEST_CONNECTION_STRING is not configured.");

        var options = new DbContextOptionsBuilder<GroupFinanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new GroupFinanceDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = Phase27WebApplicationFactory.TestAdminUsername,
            password = Phase27WebApplicationFactory.TestAdminPassword
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException($"No JSON response for GET {url}.");
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"POST {url} returned {(int)response.StatusCode} {response.StatusCode}: {error}");
        }
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException($"No JSON response for POST {url}.");
    }
}
