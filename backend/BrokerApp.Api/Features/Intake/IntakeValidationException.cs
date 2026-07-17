namespace BrokerApp.Api.Features.Intake;

public sealed class IntakeValidationException : Exception
{
    public IntakeValidationException(string message)
        : base(message)
    {
    }
}
