using BrokerApp.Api.Features.Users;

namespace BrokerApp.Api.Features.Auth;

public sealed record RegisterRequest(
    string OrganizationName,
    string DisplayName,
    string Email,
    string Password);

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

public sealed record AuthResultDto(
    CurrentUserDto? User,
    bool RequiresEmailConfirmation,
    string? DebugLink = null);

public sealed record CsrfTokenDto(string CsrfToken);
