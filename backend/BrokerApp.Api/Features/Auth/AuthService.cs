using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BrokerApp.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync();
    Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<string?> ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken = default);
    Task<string?> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IAuthEmailSender _emailSender;
    private readonly ISystemClock _clock;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthService(
        BrokerAppDbContext dbContext,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IAuthEmailSender emailSender,
        ISystemClock clock,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _clock = clock;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var organizationName = Require(request.OrganizationName, "Organization name");
        var displayName = Require(request.DisplayName, "Display name");
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var password = Require(request.Password, "Password");
        var now = _clock.UtcNow;

        if (!_environment.IsDevelopment()
            && _configuration.GetValue("Auth:RequireConfirmedEmail", true)
            && !IsRealEmailProviderConfigured())
        {
            throw new AuthValidationException("Registration is unavailable until production email is configured.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = organizationName,
            TimeZoneId = "Pacific Standard Time",
            CreatedAtUtc = now
        };
        _dbContext.Organizations.Add(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            DisplayName = displayName,
            UserName = email,
            Email = email,
            Role = UserRoles.TeamLead,
            IsActive = true,
            EmailConfirmed = !_configuration.GetValue("Auth:RequireConfirmedEmail", true),
            CreatedAtUtc = now
        };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new AuthValidationException(ToMessage(result));
        }

        await transaction.CommitAsync(cancellationToken);

        string? debugLink = null;
        if (!user.EmailConfirmed)
        {
            debugLink = await SendConfirmationAsync(user, cancellationToken);
        }
        else
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
        }

        return new AuthResultDto(
            user.EmailConfirmed ? await ToCurrentUserAsync(user.Id, cancellationToken) : null,
            !user.EmailConfirmed,
            debugLink);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var password = Require(request.Password, "Password");
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            throw new AuthValidationException("Email or password is incorrect.");
        }

        if (_configuration.GetValue("Auth:RequireConfirmedEmail", true) && !user.EmailConfirmed)
        {
            throw new AuthValidationException("Email confirmation is required before login.");
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new AuthValidationException("Account is temporarily locked. Try again later.");
        }

        if (!result.Succeeded)
        {
            throw new AuthValidationException("Email or password is incorrect.");
        }

        return new AuthResultDto(await ToCurrentUserAsync(user.Id, cancellationToken), false);
    }

    public Task LogoutAsync()
    {
        return _signInManager.SignOutAsync();
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(user);

        return Guid.TryParse(userId, out var id) ? await ToCurrentUserAsync(id, cancellationToken) : null;
    }

    public async Task<string?> ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(Require(email, "Email").ToLowerInvariant());

        if (user is null)
        {
            return null;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            throw new AuthValidationException(ToMessage(result));
        }

        return "Email confirmed. You can now log in.";
    }

    public async Task<string?> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = BuildFrontendLink("reset-password", ("email", email), ("token", token));
        await _emailSender.SendPasswordResetAsync(user, resetLink, cancellationToken);

        return _environment.IsDevelopment() ? resetLink : null;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var token = Require(request.Token, "Reset token");
        var password = Require(request.NewPassword, "New password");
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            throw new AuthValidationException("Password reset failed.");
        }

        var result = await _userManager.ResetPasswordAsync(user, token, password);

        if (!result.Succeeded)
        {
            throw new AuthValidationException(ToMessage(result));
        }
    }

    private async Task<string> SendConfirmationAsync(AppUser user, CancellationToken cancellationToken)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildFrontendLink("confirm-email", ("email", user.Email ?? string.Empty), ("token", token));
        await _emailSender.SendEmailConfirmationAsync(user, link, cancellationToken);

        return _environment.IsDevelopment() ? link : string.Empty;
    }

    private bool IsRealEmailProviderConfigured()
    {
        var provider = _configuration["Email:Provider"];

        return string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Mailgun", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<CurrentUserDto?> ToCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Organization)
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new CurrentUserDto(
                user.Id,
                user.OrganizationId,
                user.Organization.Name,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Role,
                user.IsActive,
                user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private string BuildFrontendLink(string path, params (string Name, string Value)[] values)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()?.TrimEnd('/')
            ?? "http://127.0.0.1:5173";

        return BuildLink(frontendBaseUrl, path, values);
    }

    private static string BuildLink(string baseUrl, string path, params (string Name, string Value)[] values)
    {
        var query = string.Join("&", values.Select(value =>
            $"{UrlEncoder.Default.Encode(value.Name)}={UrlEncoder.Default.Encode(value.Value)}"));

        return $"{baseUrl}/{path.TrimStart('/')}?{query}";
    }

    private static string Require(string? value, string name)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new AuthValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string ToMessage(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(error => error.Description));
    }
}
