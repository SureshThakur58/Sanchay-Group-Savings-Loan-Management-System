namespace GroupFinance.Api.Assets;

public sealed record CreateAssetRequest(
    long GroupId,
    string AssetName,
    string Category,
    DateTime AcquisitionDate,
    decimal AcquisitionCost,
    string? Location,
    string? Description,
    string PaymentMethod,
    long FinancialAccountId,
    string? ReferenceNumber);

public sealed record SellAssetRequest(
    DateTime SaleDate,
    decimal SaleAmount,
    string PaymentMethod,
    long FinancialAccountId,
    string? ReferenceNumber,
    string? Description);

public sealed record AssetResponse(
    long Id,
    long GroupId,
    string GroupCode,
    string GroupName,
    string AssetCode,
    string AssetName,
    string Category,
    DateTime AcquisitionDate,
    decimal AcquisitionCost,
    decimal BookValue,
    string? Location,
    string? Description,
    bool IsActive,
    string Status,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record AssetTransactionResponse(
    long Id,
    long AssetId,
    string AssetCode,
    string AssetName,
    DateTime TransactionDate,
    string TransactionType,
    decimal Amount,
    decimal? BookValueAtSale,
    decimal? GainLoss,
    string? Description,
    long? FinancialTransactionId,
    string? FinancialTransactionNumber,
    string Status,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);
