namespace BrokerApp.Api.Features.Customers;

public sealed class CustomerValidationException : Exception
{
    public CustomerValidationException(string message)
        : base(message)
    {
    }
}
