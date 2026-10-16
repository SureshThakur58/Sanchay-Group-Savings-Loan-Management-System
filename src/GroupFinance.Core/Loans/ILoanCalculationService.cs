namespace GroupFinance.Core.Loans;

public interface ILoanCalculationService
{
    LoanCalculationResult Calculate(LoanCalculationRequest request);
}
