namespace GroupFinance.Api.Ledger;

public sealed record LedgerEntryResponse(
    long TransactionDetailId,
    long FinancialTransactionId,
    string TransactionNumber,
    DateTime TransactionDate,
    string TransactionType,
    string? Description,
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance,
    long? MemberId,
    string? MemberNumber,
    string? MemberName,
    string? ReferenceNumber);

public sealed record TrialBalanceRowResponse(
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record TrialBalanceResponse(
    long GroupId,
    DateTime AsOfDate,
    IReadOnlyCollection<TrialBalanceRowResponse> Accounts,
    decimal TotalDebit,
    decimal TotalCredit);
