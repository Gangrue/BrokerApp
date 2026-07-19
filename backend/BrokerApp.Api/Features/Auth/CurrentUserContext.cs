using BrokerApp.Api.Domain;
using System.Security.Claims;

namespace BrokerApp.Api.Features.Auth;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    string DisplayName { get; }
    string Role { get; }
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => GetGuidClaim(ClaimTypes.NameIdentifier);
    public Guid OrganizationId => GetGuidClaim(BrokerClaims.OrganizationId);
    public string DisplayName => GetClaim(BrokerClaims.DisplayName);
    public string Role => GetClaim(ClaimTypes.Role, UserRoles.LoanOfficer);

    private Guid GetGuidClaim(string type)
    {
        var value = GetClaim(type);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Authenticated user claim {type} is invalid.");
    }

    private string GetClaim(string type, string? fallback = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var value = user?.FindFirstValue(type);

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        throw new InvalidOperationException($"Authenticated user claim {type} is missing.");
    }
}
