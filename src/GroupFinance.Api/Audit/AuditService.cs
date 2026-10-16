using System.Text.Json;
using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Audit;

public sealed class AuditValidationException(string message) : Exception(message);

public sealed class AuditService(GroupFinanceDbContext db)
{
    public async Task<IReadOnlyList<AuditLogResponse>> ListAsync(
        long groupId,
        string? entityName,
        long? entityId,
        string? action,
        long? userId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        if (groupId <= 0) throw new AuditValidationException("groupId is required.");

        var query = db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == "FinancialTransaction" &&
                        x.EntityId.HasValue &&
                        db.FinancialTransactions.Any(t =>
                            t.Id == x.EntityId.Value && t.GroupId == groupId));

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(x => x.EntityName == entityName.Trim());
        if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action.Trim());
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAtUtc >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAtUtc < toDate.Value.Date.AddDays(1));

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(1000)
            .ToListAsync(ct);

        var ids = rows.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToArray();
        var names = ids.Length == 0
            ? new Dictionary<long, string>()
            : await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        return rows.Select(x => new AuditLogResponse(
            x.Id, x.UserId, x.UserId.HasValue && names.TryGetValue(x.UserId.Value, out var name) ? name : "System",
            x.EntityName, x.EntityId, x.Action, x.OldValuesJson, x.NewValuesJson, x.Reason,
            x.IpAddress, x.CreatedAtUtc)).ToArray();
    }

    public async Task AddAsync(
        long? userId,
        string entityName,
        long? entityId,
        string action,
        object? oldValues,
        object? newValues,
        string? reason,
        string? ipAddress,
        CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = userId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Reason = reason,
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
