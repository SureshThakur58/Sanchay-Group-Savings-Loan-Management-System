namespace GroupFinance.Api.GroupFund;

public sealed record GroupFundAccountResponse(
    long Id,
    long GroupId,
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsSystemAccount,
    bool IsActive,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);

public sealed record UpdateGroupFundAccountRequest(
    string AccountName,
    string AccountType,
    bool IsActive);

public sealed record CreateOpeningFundRequest(
    long GroupId,
    long FinancialAccountId,
    decimal Amount,
    DateTime TransactionDate,
    string? ReferenceNumber,
    string? Description);

public sealed record FundTransferRequest(
    long GroupId,
    long FromFinancialAccountId,
    long ToFinancialAccountId,
    decimal Amount,
    DateTime TransactionDate,
    string? ReferenceNumber,
    string? Description);

public sealed record GroupFundSummaryResponse(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningFund,
    decimal MemberContributions,
    decimal LoanInterest,
    decimal OtherIncome,
    decimal InvestmentIncome,
    decimal TotalAvailableFund,
    decimal LoansGiven,
    decimal Expenses,
    decimal AssetPurchases,
    decimal Withdrawals,
    decimal ClosingFund,
    decimal CashBankBalance,
    decimal CashBalance,
    decimal BankBalance);

public sealed record FundTransferResponse(
    long FinancialTransactionId,
    string TransactionNumber,
    DateTime TransactionDate,
    long FromFinancialAccountId,
    long ToFinancialAccountId,
    decimal Amount,
    string Status,
    string? ReferenceNumber,
    string? Description);

public sealed record OpeningFundResponse(
    long FinancialTransactionId,
    string TransactionNumber,
    DateTime TransactionDate,
    long FinancialAccountId,
    decimal Amount,
    string Status,
    string? ReferenceNumber,
    string? Description);
