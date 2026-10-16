namespace GroupFinance.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
