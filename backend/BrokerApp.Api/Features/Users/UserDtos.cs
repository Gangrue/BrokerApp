namespace BrokerApp.Api.Features.Users;

public sealed record UserListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive,
    bool EmailConfirmed);

public sealed record CurrentUserDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive,
    bool EmailConfirmed);

public sealed record CreateUserRequest(
    string DisplayName,
    string Email,
    string Role);

public sealed record CreateUserResponseDto(
    UserListItemDto User,
    string? ConfirmationDebugLink,
    string? PasswordResetDebugLink);

public sealed record ResendUserInvitationResponseDto(
    UserListItemDto User,
    string? ConfirmationDebugLink,
    string? PasswordResetDebugLink);

public sealed record UpdateUserStatusRequest(
    bool IsActive);

public sealed record UserInvitationLinks(
    string ConfirmationLink,
    string PasswordResetLink);
