using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;

namespace BrokerApp.Api.Tests;

public sealed class TestAuthEmailSender : IAuthEmailSender
{
    public List<string> SentInvitations { get; } = [];

    public Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        SentInvitations.Add($"{user.Email}|{confirmationLink}|{resetLink}");

        return Task.CompletedTask;
    }
}
