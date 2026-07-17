namespace BrokerApp.Api.Features.Users;

public sealed record UserListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);
