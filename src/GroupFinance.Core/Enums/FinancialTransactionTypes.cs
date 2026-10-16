namespace GroupFinance.Core.Enums;

public static class FinancialTransactionTypes
{
    public const string Contribution = "CONTRIBUTION";
    public const string LoanDisbursement = "LOAN_DISBURSEMENT";
    public const string LoanRepayment = "LOAN_REPAYMENT";
    public const string InterestIncome = "INTEREST_INCOME";
    public const string InterestDistribution = "INTEREST_DISTRIBUTION";
    public const string Expense = "EXPENSE";
    public const string AssetPurchase = "ASSET_PURCHASE";
    public const string AssetSale = "ASSET_SALE";
    public const string Transfer = "TRANSFER";
    public const string Adjustment = "ADJUSTMENT";

    public static readonly string[] All =
    [
        Contribution,
        LoanDisbursement,
        LoanRepayment,
        InterestIncome,
        InterestDistribution,
        Expense,
        AssetPurchase,
        AssetSale,
        Transfer,
        Adjustment
    ];
}
