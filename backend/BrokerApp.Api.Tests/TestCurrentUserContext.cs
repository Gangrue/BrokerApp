using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;

namespace BrokerApp.Api.Tests;

public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public static readonly TestCurrentUserContext Instance = new();

    public static readonly TestCurrentUserContext TeamLead = new()
    {
        UserId = DevDataIds.TeamLeadId,
        DisplayName = "Test Team Lead",
        Role = UserRoles.TeamLead
    };

    public Guid UserId { get; init; } = DevDataIds.LoanOfficerId;

    public Guid OrganizationId { get; init; } = DevDataIds.OrganizationId;

    public string DisplayName { get; init; } = "Test Loan Officer";

    public string Role { get; init; } = UserRoles.LoanOfficer;
}
