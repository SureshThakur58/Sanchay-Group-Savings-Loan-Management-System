using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroupFinance.Api.FileStorage;

public sealed class AttachmentValidationException(string message) : Exception(message);

public sealed class AttachmentService(
    GroupFinanceDbContext db,
    IOptions<AttachmentOptions> options,
    IWebHostEnvironment environment)
{
    private static readonly HashSet<string> AllowedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Group", "Member", "ContributionSchedule", "ContributionPayment",
        "LoanApplication", "Loan", "LoanPayment", "FinancialTransaction",
        "ProfitDistribution", "Expense", "Asset", "AssetTransaction"
    };

    private readonly AttachmentOptions _options = options.Value;

    public async Task<IReadOnlyList<AttachmentResponse>> ListAsync(
        string entityName,
        long entityId,
        CancellationToken ct)
    {
        var normalized = NormalizeEntity(entityName);
        await EnsureEntityExistsAsync(normalized, entityId, ct);

        return await db.Attachments.AsNoTracking()
            .Where(x => x.EntityName == normalized && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new AttachmentResponse(
                x.Id, x.EntityName, x.EntityId, x.OriginalFileName,
                x.ContentType, x.FileSizeBytes, x.CreatedAtUtc, x.CreatedByUserId))
            .ToListAsync(ct);
    }

    public async Task<AttachmentResponse> UploadAsync(
        string entityName,
        long entityId,
        IFormFile? file,
        long? userId,
        CancellationToken ct)
    {
        var normalized = NormalizeEntity(entityName);
        await EnsureEntityExistsAsync(normalized, entityId, ct);

        if (file is null)
            throw new AttachmentValidationException("A non-empty file is required.");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType.Trim();

        var originalName = AttachmentFileValidator.Validate(
            file.FileName,
            contentType,
            file.Length,
            _options.MaxFileSizeBytes,
            _options.AllowedExtensions);

        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var expectedContentType = AttachmentFileValidator.AllowedContentTypes[extension];

        await using var input = file.OpenReadStream();
        var header = new byte[8];
        var read = await input.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (!AttachmentFileValidator.HasValidSignature(header.AsSpan(0, read), extension))
            throw new AttachmentValidationException("The uploaded file content does not match its declared file type.");

        var now = DateTime.UtcNow;
        var relativeDirectory = Path.Combine(
            now.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            now.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
            normalized);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(relativeDirectory, storedFileName);
        var root = GetStorageRoot();
        var fullDirectory = GetSafeFullPath(root, relativeDirectory);
        var fullPath = GetSafeFullPath(root, relativePath);

        Directory.CreateDirectory(fullDirectory);

        try
        {
            await using (var output = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(header.AsMemory(0, read), ct);
                await input.CopyToAsync(output, ct);
            }

            var attachment = new Attachment
            {
                EntityName = normalized,
                EntityId = entityId,
                OriginalFileName = originalName,
                StoredFileName = storedFileName,
                ContentType = expectedContentType,
                FileSizeBytes = file.Length,
                StoragePath = relativePath.Replace('\\', '/'),
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };

            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(ct);

            return new AttachmentResponse(
                attachment.Id, attachment.EntityName, attachment.EntityId,
                attachment.OriginalFileName, attachment.ContentType,
                attachment.FileSizeBytes, attachment.CreatedAtUtc,
                attachment.CreatedByUserId);
        }
        catch
        {
            TryDelete(fullPath);
            throw;
        }
    }

    public async Task<AttachmentDownload?> OpenDownloadAsync(long id, CancellationToken ct)
    {
        if (id <= 0)
            throw new AttachmentValidationException("Attachment id is required.");

        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (attachment is null)
            return null;

        var root = GetStorageRoot();
        var fullPath = GetSafeFullPath(root, attachment.StoragePath);
        if (!File.Exists(fullPath))
            throw new AttachmentValidationException("The stored attachment file could not be found.");

        var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return new AttachmentDownload(
            attachment.OriginalFileName,
            attachment.ContentType,
            stream.Length,
            stream);
    }

    public async Task DeleteAsync(long id, long? userId, CancellationToken ct)
    {
        if (id <= 0)
            throw new AttachmentValidationException("Attachment id is required.");

        var attachment = await db.Attachments
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (attachment is null)
            return;

        var root = GetStorageRoot();
        var fullPath = GetSafeFullPath(root, attachment.StoragePath);

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);

        TryDelete(fullPath);
    }

    private string NormalizeEntity(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            throw new AttachmentValidationException("entityName is required.");

        var normalized = entityName.Trim();
        if (!AllowedEntities.Contains(normalized))
            throw new AttachmentValidationException(
                $"Attachments are not supported for entity '{normalized}'.");

        return AllowedEntities.First(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureEntityExistsAsync(string entityName, long entityId, CancellationToken ct)
    {
        if (entityId <= 0)
            throw new AttachmentValidationException("entityId is required.");

        var exists = entityName switch
        {
            "Group" => await db.Groups.AnyAsync(x => x.Id == entityId, ct),
            "Member" => await db.Members.AnyAsync(x => x.Id == entityId, ct),
            "ContributionSchedule" => await db.ContributionSchedules.AnyAsync(x => x.Id == entityId, ct),
            "ContributionPayment" => await db.ContributionPayments.AnyAsync(x => x.Id == entityId, ct),
            "LoanApplication" => await db.LoanApplications.AnyAsync(x => x.Id == entityId, ct),
            "Loan" => await db.Loans.AnyAsync(x => x.Id == entityId, ct),
            "LoanPayment" => await db.LoanPayments.AnyAsync(x => x.Id == entityId, ct),
            "FinancialTransaction" => await db.FinancialTransactions.AnyAsync(x => x.Id == entityId, ct),
            "ProfitDistribution" => await db.ProfitDistributions.AnyAsync(x => x.Id == entityId, ct),
            "Expense" => await db.Expenses.AnyAsync(x => x.Id == entityId, ct),
            "Asset" => await db.Assets.AnyAsync(x => x.Id == entityId, ct),
            "AssetTransaction" => await db.AssetTransactions.AnyAsync(x => x.Id == entityId, ct),
            _ => false
        };

        if (!exists)
            throw new AttachmentValidationException(
                $"The {entityName} record with id {entityId} does not exist.");
    }

    private string GetStorageRoot()
    {
        var configured = _options.RootPath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("FileStorage:RootPath is not configured.");

        return Path.GetFullPath(
            Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured));
    }

    private static string GetSafeFullPath(string root, string relativePath)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new AttachmentValidationException("Invalid attachment storage path.");

        return candidate;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
    }
}
