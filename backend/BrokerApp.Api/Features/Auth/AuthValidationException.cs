namespace BrokerApp.Api.Features.Auth;

public sealed class AuthValidationException : Exception
{
    public AuthValidationException(string message)
        : base(message)
    {
    }
}
