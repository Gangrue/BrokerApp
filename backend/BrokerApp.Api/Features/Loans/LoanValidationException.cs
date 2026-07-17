namespace BrokerApp.Api.Features.Loans;

public sealed class LoanValidationException : Exception
{
    public LoanValidationException(string message)
        : base(message)
    {
    }
}
