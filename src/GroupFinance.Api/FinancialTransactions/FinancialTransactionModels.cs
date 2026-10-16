namespace GroupFinance.Api.FinancialTransactions;

public sealed record FinancialTransactionLineRequest(
    long FinancialAccountId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

public sealed record CreateFinancialTransactionRequest(
    long GroupId,
    long? MemberId,
    DateTime TransactionDate,
    string TransactionType,
    string? Description,
    string? ReferenceType,
    long? ReferenceId,
    string? ReferenceNumber,
    IReadOnlyCollection<FinancialTransactionLineRequest> Lines);

public sealed record FinancialTransactionLineResponse(
    long Id,
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

public sealed record FinancialTransactionResponse(
    long Id,
    string TransactionNumber,
    DateTime TransactionDate,
    long GroupId,
    long? MemberId,
    string TransactionType,
    string Status,
    string? Description,
    string? ReferenceType,
    long? ReferenceId,
    string? ReferenceNumber,
    decimal Amount,
    decimal TotalDebit,
    decimal TotalCredit,
    long? CreatedByUserId,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<FinancialTransactionLineResponse> Lines);

public sealed record FinancialAccountResponse(
    long Id,
    long GroupId,
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsSystemAccount,
    bool IsActive);

public sealed record CreateFinancialAccountRequest(
    long GroupId,
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsSystemAccount = false);

public sealed record FinancialAccountBalanceResponse(
    long FinancialAccountId,
    string AccountCode,
    string AccountName,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);
