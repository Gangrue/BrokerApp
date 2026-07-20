using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BrokerApp.Api.Features.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<CreateUserResponseDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<ResendUserInvitationResponseDto> ResendInvitationAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserListItemDto> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task<UserListItemDto> UpdateSidebarItemsAsync(Guid userId, UpdateUserSidebarRequest request, CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private static readonly string[] DefaultVisibleSidebarItems =
    [
        "home",
        "triage",
        "dashboard",
        "loans",
        "customers",
        "import",
        "reports",
        "admin",
        "account"
    ];
    private static readonly HashSet<string> AllowedSidebarItems = new(DefaultVisibleSidebarItems, StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BrokerAppDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuthEmailSender _emailSender;
    private readonly IAuditWriter _auditWriter;
    private readonly ISystemClock _clock;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public UserService(
        BrokerAppDbContext dbContext,
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUser,
        IAuthEmailSender emailSender,
        IAuditWriter auditWriter,
        ISystemClock clock,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _currentUser = currentUser;
        _emailSender = emailSender;
        _auditWriter = auditWriter;
        _clock = clock;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<IReadOnlyCollection<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.OrganizationId == _currentUser.OrganizationId)
            .OrderBy(user => user.DisplayName)
            .ToArrayAsync(cancellationToken);

        return users.Select(ToListItem).ToArray();
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Organization)
            .SingleOrDefaultAsync(
                user => user.OrganizationId == _currentUser.OrganizationId && user.Id == _currentUser.UserId,
                cancellationToken);

        return user is null
            ? null
            : new CurrentUserDto(
                user.Id,
                user.OrganizationId,
                user.Organization.Name,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Role,
                user.IsActive,
                user.EmailConfirmed,
                VisibleSidebarItems(user));
    }

    public async Task<CreateUserResponseDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRoles.TeamLead)
        {
            throw new UnauthorizedAccessException("Only Team Leads can create users.");
        }

        var displayName = Require(request.DisplayName, "Display name");
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var role = NormalizeRole(request.Role);

        if (await _dbContext.Users.AnyAsync(
            user => user.OrganizationId == _currentUser.OrganizationId
                && user.Email != null
                && user.Email.ToLower() == email,
            cancellationToken))
        {
            throw new UserValidationException("A user with that email already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _currentUser.OrganizationId,
            DisplayName = displayName,
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            Role = role,
            IsActive = true,
            VisibleSidebarItemsJson = SerializeSidebarItems(DefaultVisibleSidebarItems),
            CreatedAtUtc = _clock.UtcNow
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new UserValidationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        var links = await CreateInvitationLinksAsync(user);
        await _emailSender.SendUserInvitationAsync(user, links.ConfirmationLink, links.PasswordResetLink, cancellationToken);

        return new CreateUserResponseDto(
            new UserListItemDto(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Role,
                user.IsActive,
                user.EmailConfirmed,
                VisibleSidebarItems(user)),
            _environment.IsDevelopment() ? links.ConfirmationLink : null,
            _environment.IsDevelopment() ? links.PasswordResetLink : null);
    }

    public async Task<UserListItemDto> UpdateSidebarItemsAsync(
        Guid userId,
        UpdateUserSidebarRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRoles.TeamLead)
        {
            throw new UnauthorizedAccessException("Only Team Leads can manage user sidebar visibility.");
        }

        var user = await _dbContext.Users
            .Where(item => item.OrganizationId == _currentUser.OrganizationId && item.Id == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UserValidationException("User was not found.");
        var visibleItems = NormalizeSidebarItems(request.VisibleSidebarItems, user.Id == _currentUser.UserId, user.Role);
        user.VisibleSidebarItemsJson = SerializeSidebarItems(visibleItems);

        _auditWriter.Record(
            "User",
            user.Id.ToString(),
            AuditOperations.Updated,
            "Sidebar navigation visibility updated.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToListItem(user);
    }

    public async Task<UserListItemDto> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRoles.TeamLead)
        {
            throw new UnauthorizedAccessException("Only Team Leads can manage users.");
        }

        if (userId == _currentUser.UserId && !isActive)
        {
            throw new UserValidationException("You cannot remove your own active account.");
        }

        var user = await _dbContext.Users
            .Where(item => item.OrganizationId == _currentUser.OrganizationId && item.Id == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UserValidationException("User was not found.");

        if (user.IsActive == isActive)
        {
            return ToListItem(user);
        }

        user.IsActive = isActive;
        var loginLink = isActive ? BuildFrontendLink("login") : string.Empty;
        _auditWriter.Record(
            "User",
            user.Id.ToString(),
            AuditOperations.Updated,
            isActive ? "User re-enabled." : user.EmailConfirmed ? "User removed." : "Invitation cancelled.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isActive)
        {
            await _emailSender.SendUserReEnabledAsync(user, loginLink, cancellationToken);
        }

        return ToListItem(user);
    }

    public async Task<ResendUserInvitationResponseDto> ResendInvitationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRoles.TeamLead)
        {
            throw new UnauthorizedAccessException("Only Team Leads can manage users.");
        }

        var user = await _dbContext.Users
            .Where(item => item.OrganizationId == _currentUser.OrganizationId && item.Id == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UserValidationException("User was not found.");

        if (!user.IsActive)
        {
            throw new UserValidationException("Re-enable this user before resending setup links.");
        }

        if (user.EmailConfirmed)
        {
            throw new UserValidationException("Only pending invitations can be resent.");
        }

        var links = await CreateInvitationLinksAsync(user);
        await _emailSender.SendUserInvitationAsync(user, links.ConfirmationLink, links.PasswordResetLink, cancellationToken);

        _auditWriter.Record(
            "User",
            user.Id.ToString(),
            AuditOperations.Updated,
            "Invitation resent.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ResendUserInvitationResponseDto(
            ToListItem(user),
            _environment.IsDevelopment() ? links.ConfirmationLink : null,
            _environment.IsDevelopment() ? links.PasswordResetLink : null);
    }

    private async Task<UserInvitationLinks> CreateInvitationLinksAsync(AppUser user)
    {
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        return new UserInvitationLinks(
            BuildFrontendLink("confirm-email", ("email", user.Email ?? string.Empty), ("token", confirmationToken)),
            BuildFrontendLink("reset-password", ("email", user.Email ?? string.Empty), ("token", resetToken)));
    }

    private string BuildFrontendLink(string path, params (string Name, string Value)[] values)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()?.TrimEnd('/')
            ?? "http://127.0.0.1:5173";
        var query = string.Join("&", values.Select(value =>
            $"{UrlEncoder.Default.Encode(value.Name)}={UrlEncoder.Default.Encode(value.Value)}"));

        return string.IsNullOrWhiteSpace(query)
            ? $"{frontendBaseUrl}/{path.TrimStart('/')}"
            : $"{frontendBaseUrl}/{path.TrimStart('/')}?{query}";
    }

    private static string NormalizeRole(string? role)
    {
        var trimmed = Require(role, "Role");

        return trimmed switch
        {
            UserRoles.LoanOfficer => UserRoles.LoanOfficer,
            UserRoles.TeamLead => UserRoles.TeamLead,
            _ => throw new UserValidationException("Role must be Loan Officer or Team Lead.")
        };
    }

    private static string Require(string? value, string name)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed)
            ? throw new UserValidationException($"{name} is required.")
            : trimmed;
    }

    private static UserListItemDto ToListItem(AppUser user)
    {
        return new UserListItemDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.Role,
            user.IsActive,
            user.EmailConfirmed,
            VisibleSidebarItems(user));
    }

    private static IReadOnlyCollection<string> NormalizeSidebarItems(
        IReadOnlyCollection<string>? visibleItems,
        bool isCurrentUser,
        string role)
    {
        var normalized = (visibleItems ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .Where(item => AllowedSidebarItems.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        EnsureVisible(normalized, "home");
        EnsureVisible(normalized, "account");

        if (isCurrentUser && role == UserRoles.TeamLead)
        {
            EnsureVisible(normalized, "admin");
        }

        return DefaultVisibleSidebarItems
            .Where(item => normalized.Contains(item, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void EnsureVisible(List<string> visibleItems, string item)
    {
        if (!visibleItems.Contains(item, StringComparer.OrdinalIgnoreCase))
        {
            visibleItems.Add(item);
        }
    }

    private static IReadOnlyCollection<string> VisibleSidebarItems(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(user.VisibleSidebarItemsJson))
        {
            return DefaultVisibleSidebarItems;
        }

        try
        {
            var items = JsonSerializer.Deserialize<IReadOnlyCollection<string>>(user.VisibleSidebarItemsJson, JsonOptions);
            return NormalizeSidebarItems(items, false, user.Role);
        }
        catch (JsonException)
        {
            return DefaultVisibleSidebarItems;
        }
    }

    private static string SerializeSidebarItems(IReadOnlyCollection<string> visibleItems)
    {
        return JsonSerializer.Serialize(visibleItems, JsonOptions);
    }
}

public sealed class UserValidationException : Exception
{
    public UserValidationException(string message)
        : base(message)
    {
    }
}
