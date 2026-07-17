namespace BrokerApp.Api.Features.ActionTemplates;

public sealed class ActionTemplateValidationException : Exception
{
    public ActionTemplateValidationException(string message)
        : base(message)
    {
    }
}
