using System.Security.Claims;
using System.Text.Encodings.Web;
using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace BrokerApp.Api.Tests;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, DevDataIds.LoanOfficerId.ToString()),
            new Claim(BrokerClaims.OrganizationId, DevDataIds.OrganizationId.ToString()),
            new Claim(BrokerClaims.DisplayName, "Test Loan Officer"),
            new Claim(ClaimTypes.Name, "Test Loan Officer"),
            new Claim(ClaimTypes.Role, UserRoles.LoanOfficer)
        ];

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
