namespace GroupFinance.Api.Models;

public sealed record HealthResponse(
    string Status,
    string Application,
    string Environment,
    DateTime UtcTime);
