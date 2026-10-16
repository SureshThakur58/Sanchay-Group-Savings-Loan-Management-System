using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Assets;

public sealed class AssetService(GroupFinanceDbContext db, FinancialTransactionService financialTransactions)
{
    private static readonly string[] AllowedCategories =
    [
        "Property", "Land", "Vehicle", "Computer", "Furniture", "Equipment", "Investment", "Other"
    ];

    private static readonly string[] PaymentMethods = ["Cash", "Bank", "Online Transfer", "Other"];
    private static readonly string[] FundAccountTypes = ["Cash", "Bank", "Other"];

    public async Task<IReadOnlyCollection<AssetResponse>> ListAssetsAsync(
        long groupId,
        bool includeInactive,
        string? category,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);

        var query = db.Assets.AsNoTracking()
            .Include(x => x.Group)
            .Where(x => x.GroupId == groupId);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == NormalizeCategory(category));

        var rows = await query
            .OrderBy(x => x.AssetCode)
            .Take(1000)
            .ToArrayAsync(ct);

        return rows.Select(MapAsset).ToArray();
    }

    public async Task<AssetResponse?> GetAssetAsync(long id, CancellationToken ct)
    {
        var row = await db.Assets.AsNoTracking()
            .Include(x => x.Group)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : MapAsset(row);
    }

    public async Task<IReadOnlyCollection<AssetTransactionResponse>> ListTransactionsAsync(long assetId, CancellationToken ct)
    {
        var rows = await db.AssetTransactions.AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.FinancialTransaction)
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Take(1000)
            .ToArrayAsync(ct);

        if (rows.Length == 0 && !await db.Assets.AnyAsync(x => x.Id == assetId, ct))
            throw new AssetValidationException("Asset not found.");

        return rows.Select(MapTransaction).ToArray();
    }

    public async Task<AssetResponse> CreateAssetAsync(CreateAssetRequest request, long userId, CancellationToken ct)
    {
        ValidateAmount(request.AcquisitionCost, "Acquisition cost");
        var category = NormalizeCategory(request.Category);
        var assetName = NormalizeRequired(request.AssetName, "Asset name", 200);
        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var location = NormalizeOptional(request.Location, 500);
        var description = NormalizeOptional(request.Description, 1000);
        var referenceNumber = NormalizeOptional(request.ReferenceNumber, 100);

        await EnsureGroupAsync(request.GroupId, ct);
        if (request.AcquisitionDate == default)
            throw new AssetValidationException("Acquisition date is required.");

        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

            var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, ct)
                ?? throw new AssetValidationException("The selected group does not exist or is inactive.");

            var paymentAccount = await GetActiveFundAccountAsync(request.GroupId, request.FinancialAccountId, ct);
            var balance = await GetAccountBalanceAsync(paymentAccount.Id, ct);
            if (balance < request.AcquisitionCost)
                throw new AssetValidationException($"Insufficient available balance in the selected payment account. Available balance is {balance:N2}.");

            var now = DateTime.UtcNow;
            var asset = new Asset
            {
                GroupId = request.GroupId,
                AssetCode = $"TMP-{Guid.NewGuid():N}"[..30],
                AssetName = assetName,
                Category = category,
                AcquisitionDate = request.AcquisitionDate,
                AcquisitionCost = RoundMoney(request.AcquisitionCost),
                BookValue = RoundMoney(request.AcquisitionCost),
                Location = location,
                Description = description,
                IsActive = true,
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };

            db.Assets.Add(asset);
            await db.SaveChangesAsync(ct);
            asset.AssetCode = $"AST-{asset.Id:00000000}";

            var assetAccount = await EnsureAssetAccountAsync(request.GroupId, asset, userId, ct);
            var posted = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    request.GroupId,
                    null,
                    request.AcquisitionDate,
                    FinancialTransactionTypes.AssetPurchase,
                    description ?? $"Asset purchase - {asset.AssetName}",
                    "ASSET_PURCHASE",
                    asset.Id,
                    asset.AssetCode,
                    [
                        new FinancialTransactionLineRequest(assetAccount.Id, asset.AcquisitionCost, 0m, $"Asset purchase - {asset.AssetName}"),
                        new FinancialTransactionLineRequest(paymentAccount.Id, 0m, asset.AcquisitionCost, $"Asset paid by {paymentMethod}")
                    ]), userId, ct);

            db.AssetTransactions.Add(new AssetTransaction
            {
                AssetId = asset.Id,
                TransactionDate = request.AcquisitionDate,
                TransactionType = AssetTransactionTypes.Purchase,
                Amount = asset.AcquisitionCost,
                Description = description ?? $"Asset purchase - {asset.AssetName}",
                FinancialTransactionId = posted.Id,
                CreatedAtUtc = now,
                CreatedByUserId = userId
            });

            asset.ModifiedAtUtc = now;
            asset.ModifiedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            return MapAsset(asset, group);
        });
    }

    public async Task<AssetTransactionResponse> SellAssetAsync(long assetId, SellAssetRequest request, long userId, CancellationToken ct)
    {
        ValidateAmount(request.SaleAmount, "Sale amount");
        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var referenceNumber = NormalizeOptional(request.ReferenceNumber, 100);
        var description = NormalizeOptional(request.Description, 500);

        if (request.SaleDate == default)
            throw new AssetValidationException("Sale date is required.");

        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

            var asset = await db.Assets
                .Include(x => x.Group)
                .SingleOrDefaultAsync(x => x.Id == assetId, ct)
                ?? throw new AssetValidationException("Asset not found.");

            await EnsureGroupAsync(asset.GroupId, ct);
            if (!asset.IsActive)
                throw new AssetValidationException("Only active assets can be sold.");
            if (request.SaleDate.Date < asset.AcquisitionDate.Date)
                throw new AssetValidationException("Sale date cannot be earlier than the acquisition date.");
            if (asset.BookValue <= 0m)
                throw new AssetValidationException("The asset has no positive book value available for sale.");

            var paymentAccount = await GetActiveFundAccountAsync(asset.GroupId, request.FinancialAccountId, ct);
            var assetAccount = await EnsureAssetAccountAsync(asset.GroupId, asset, userId, ct);
            var saleAmount = RoundMoney(request.SaleAmount);
            var bookValue = RoundMoney(asset.BookValue);
            var gainLoss = RoundMoney(saleAmount - bookValue);
            var transactionDescription = description ?? $"Asset sale - {asset.AssetName}";

            var lines = new List<FinancialTransactionLineRequest>
            {
                new(paymentAccount.Id, saleAmount, 0m, $"Asset sale proceeds paid by {paymentMethod}"),
            };

            if (gainLoss > 0m)
            {
                var gainAccount = await EnsureIncomeAccountAsync(asset.GroupId, "ASSET-SALE-GAIN", "Asset Sale Gain", userId, ct);
                lines.Add(new FinancialTransactionLineRequest(assetAccount.Id, 0m, bookValue, $"Remove asset book value - {asset.AssetName}"));
                lines.Add(new FinancialTransactionLineRequest(gainAccount.Id, 0m, gainLoss, "Gain on asset sale"));
            }
            else if (gainLoss < 0m)
            {
                var lossAccount = await EnsureExpenseAccountAsync(asset.GroupId, "ASSET-SALE-LOSS", "Asset Sale Loss", userId, ct);
                lines.Add(new FinancialTransactionLineRequest(lossAccount.Id, Math.Abs(gainLoss), 0m, "Loss on asset sale"));
                lines.Add(new FinancialTransactionLineRequest(assetAccount.Id, 0m, bookValue, $"Remove asset book value - {asset.AssetName}"));
            }
            else
            {
                lines.Add(new FinancialTransactionLineRequest(assetAccount.Id, 0m, bookValue, $"Remove asset book value - {asset.AssetName}"));
            }

            var posted = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    asset.GroupId,
                    null,
                    request.SaleDate,
                    FinancialTransactionTypes.AssetSale,
                    transactionDescription,
                    "ASSET_SALE",
                    asset.Id,
                    asset.AssetCode,
                    lines), userId, ct);

            var now = DateTime.UtcNow;
            asset.BookValue = 0m;
            asset.IsActive = false;
            asset.ModifiedAtUtc = now;
            asset.ModifiedByUserId = userId;

            var assetTransaction = new AssetTransaction
            {
                AssetId = asset.Id,
                TransactionDate = request.SaleDate,
                TransactionType = AssetTransactionTypes.Sale,
                Amount = saleAmount,
                BookValueAtSale = bookValue,
                GainLoss = gainLoss,
                Description = transactionDescription,
                FinancialTransactionId = posted.Id,
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            db.AssetTransactions.Add(assetTransaction);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            return new AssetTransactionResponse(
                assetTransaction.Id,
                asset.Id,
                asset.AssetCode,
                asset.AssetName,
                assetTransaction.TransactionDate,
                assetTransaction.TransactionType,
                assetTransaction.Amount,
                assetTransaction.BookValueAtSale,
                assetTransaction.GainLoss,
                assetTransaction.Description,
                posted.Id,
                posted.TransactionNumber,
                posted.Status,
                assetTransaction.CreatedAtUtc,
                assetTransaction.CreatedByUserId);
        });
    }

    private async Task<FinancialAccount> EnsureAssetAccountAsync(long groupId, Asset asset, long userId, CancellationToken ct)
    {
        var code = $"AST-{asset.Id:00000000}";
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null)
        {
            if (!account.AccountType.Equals("Asset", StringComparison.OrdinalIgnoreCase))
                throw new AssetValidationException("The asset financial account has an invalid account type.");
            if (!account.IsActive)
                throw new AssetValidationException("The asset financial account is inactive.");
            return account;
        }

        account = new FinancialAccount
        {
            GroupId = groupId,
            AccountCode = code,
            AccountName = $"Asset - {asset.AssetName}",
            AccountType = "Asset",
            IsSystemAccount = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<FinancialAccount> EnsureIncomeAccountAsync(long groupId, string code, string name, long userId, CancellationToken ct)
        => await EnsureSystemAccountAsync(groupId, code, name, "Income", userId, ct);

    private async Task<FinancialAccount> EnsureExpenseAccountAsync(long groupId, string code, string name, long userId, CancellationToken ct)
        => await EnsureSystemAccountAsync(groupId, code, name, "Expense", userId, ct);

    private async Task<FinancialAccount> EnsureSystemAccountAsync(long groupId, string code, string name, string type, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null)
        {
            if (!account.AccountType.Equals(type, StringComparison.OrdinalIgnoreCase))
                throw new AssetValidationException($"The system financial account '{code}' has an invalid account type.");
            if (!account.IsActive)
                throw new AssetValidationException($"The system financial account '{code}' is inactive.");
            return account;
        }

        account = new FinancialAccount
        {
            GroupId = groupId,
            AccountCode = code,
            AccountName = name,
            AccountType = type,
            IsSystemAccount = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<FinancialAccount> GetActiveFundAccountAsync(long groupId, long accountId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Id == accountId && x.GroupId == groupId && x.IsActive, ct)
            ?? throw new AssetValidationException("The selected financial account does not exist, is inactive, or belongs to another group.");
        if (!FundAccountTypes.Contains(account.AccountType, StringComparer.OrdinalIgnoreCase))
            throw new AssetValidationException("The payment account must be a Cash, Bank, or Other fund account.");
        return account;
    }

    private async Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken ct)
        => await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0 || !await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new AssetValidationException("The selected group does not exist or is inactive.");

        var activeGroupCount = await db.Groups.CountAsync(x => x.IsActive, ct);
        if (activeGroupCount != 1)
            throw new AssetValidationException("The system requires exactly one active configured group.");
    }

    private static AssetResponse MapAsset(Asset asset)
        => MapAsset(asset, asset.Group);

    private static AssetResponse MapAsset(Asset asset, Group group)
        => new(
            asset.Id,
            asset.GroupId,
            group.Code,
            group.Name,
            asset.AssetCode,
            asset.AssetName,
            asset.Category,
            asset.AcquisitionDate,
            asset.AcquisitionCost,
            asset.BookValue,
            asset.Location,
            asset.Description,
            asset.IsActive,
            asset.IsActive ? "Active" : "Sold",
            asset.CreatedAtUtc,
            asset.CreatedByUserId,
            asset.ModifiedAtUtc,
            asset.ModifiedByUserId);

    private static AssetTransactionResponse MapTransaction(AssetTransaction row)
        => new(
            row.Id,
            row.AssetId,
            row.Asset.AssetCode,
            row.Asset.AssetName,
            row.TransactionDate,
            row.TransactionType,
            row.Amount,
            row.BookValueAtSale,
            row.GainLoss,
            row.Description,
            row.FinancialTransactionId,
            row.FinancialTransaction?.TransactionNumber,
            row.FinancialTransaction?.Status ?? "Posted",
            row.CreatedAtUtc,
            row.CreatedByUserId);

    private static string NormalizeCategory(string? value)
    {
        var result = value?.Trim() ?? string.Empty;
        var category = AllowedCategories.FirstOrDefault(x => x.Equals(result, StringComparison.OrdinalIgnoreCase));
        return category ?? throw new AssetValidationException("Asset category must be Property, Land, Vehicle, Computer, Furniture, Equipment, Investment, or Other.");
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var result = value?.Trim() ?? string.Empty;
        var method = PaymentMethods.FirstOrDefault(x => x.Equals(result, StringComparison.OrdinalIgnoreCase));
        return method ?? throw new AssetValidationException("Payment method must be Cash, Bank, Online Transfer, or Other.");
    }

    private static string NormalizeRequired(string? value, string field, int maxLength)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new AssetValidationException($"{field} is required.");
        if (result.Length > maxLength) throw new AssetValidationException($"{field} cannot exceed {maxLength} characters.");
        return result;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > maxLength) throw new AssetValidationException($"Value cannot exceed {maxLength} characters.");
        return result;
    }

    private static void ValidateAmount(decimal amount, string field)
    {
        if (amount <= 0m) throw new AssetValidationException($"{field} must be greater than zero.");
        if (amount > 9999999999999999.99m) throw new AssetValidationException($"{field} is too large.");
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class AssetValidationException(string message) : Exception(message);
