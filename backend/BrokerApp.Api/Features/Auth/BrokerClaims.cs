using System.Security.Claims;

namespace BrokerApp.Api.Features.Auth;

public static class BrokerClaims
{
    public const string OrganizationId = "broker_app:organization_id";
    public const string OrganizationName = "broker_app:organization_name";
    public const string DisplayName = ClaimTypes.Name;
}
