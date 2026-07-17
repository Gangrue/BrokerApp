using BrokerApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private readonly BrokerAppDbContext _dbContext;

    public UserService(BrokerAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.OrganizationId == DevDataIds.OrganizationId)
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserListItemDto(
                user.Id,
                user.DisplayName,
                user.Email,
                user.Role,
                user.IsActive))
            .ToArrayAsync(cancellationToken);
    }
}
