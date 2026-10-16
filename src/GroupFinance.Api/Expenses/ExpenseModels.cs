namespace GroupFinance.Api.Expenses;

public sealed record CreateExpenseCategoryRequest(long GroupId, string Name, bool IsActive = true);
public sealed record UpdateExpenseCategoryRequest(string Name, bool IsActive);
public sealed record ExpenseCategoryResponse(long Id, long GroupId, string GroupCode, string GroupName, string Name, bool IsActive, DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);

public sealed record CreateExpenseRequest(
    long GroupId,
    long ExpenseCategoryId,
    DateTime ExpenseDate,
    decimal Amount,
    string PaymentMethod,
    long FinancialAccountId,
    string? ReferenceNumber,
    string Description);

public sealed record ExpenseResponse(
    long Id,
    string ExpenseNumber,
    long GroupId,
    string GroupCode,
    string GroupName,
    long ExpenseCategoryId,
    string ExpenseCategoryName,
    DateTime ExpenseDate,
    decimal Amount,
    string PaymentMethod,
    long FinancialAccountId,
    string FinancialAccountCode,
    string FinancialAccountName,
    string? ReferenceNumber,
    string Description,
    string Status,
    long? FinancialTransactionId,
    string? FinancialTransactionNumber,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);
