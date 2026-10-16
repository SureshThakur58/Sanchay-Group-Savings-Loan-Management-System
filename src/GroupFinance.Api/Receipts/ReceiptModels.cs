namespace GroupFinance.Api.Receipts;

public sealed record ReceiptResponse(
    long FinancialTransactionId,
    string ReceiptType,
    long GroupId,
    string GroupCode,
    string GroupName,
    long? MemberId,
    string? MemberNumber,
    string? MemberName,
    string TransactionNumber,
    DateTime TransactionDate,
    decimal Amount,
    string? PaymentMethod,
    string? Description,
    string? ReferenceNumber,
    string PreparedBy,
    string Status,
    IReadOnlyCollection<ReceiptLineResponse> Lines);

public sealed record ReceiptLineResponse(
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

public static class ReceiptTypeResolver
{
    public static string? Resolve(string transactionType) =>
        transactionType.Trim().ToUpperInvariant() switch
        {
            "CONTRIBUTION" => "Contribution Receipt",
            "LOAN_DISBURSEMENT" => "Loan Disbursement Receipt",
            "LOAN_REPAYMENT" => "Loan Repayment Receipt",
            "EXPENSE" => "Expense Voucher",
            "ASSET_PURCHASE" => "Asset Purchase Voucher",
            "INTEREST_DISTRIBUTION" => "Profit Distribution Voucher",
            _ => null
        };
}
