using BrokerApp.Api.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;

namespace BrokerApp.Api.Features.Auth;

public interface IAuthEmailSender
{
    Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken);
    Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken);
}

public sealed record AuthEmailContent(string Subject, string TextBody, string HtmlBody);

public static class AuthEmailTemplates
{
    public static AuthEmailContent EmailConfirmation(AppUser user, string confirmationLink, IConfiguration configuration)
    {
        return Build(
            configuration,
            "Confirm your LobiLend email",
            "Welcome to LobiLend",
            "Confirm your email address to finish setting up your account and open your loan workflow dashboard.",
            "Confirm email",
            confirmationLink,
            "If you did not create a LobiLend account, you can safely ignore this email.",
            $"Hi {DisplayName(user)},\n\nWelcome to LobiLend.\n\nConfirm your email address to finish setting up your account and open your loan workflow dashboard:\n\n{confirmationLink}\n\nIf you did not create a LobiLend account, you can safely ignore this email.");
    }

    public static AuthEmailContent PasswordReset(AppUser user, string resetLink, IConfiguration configuration)
    {
        return Build(
            configuration,
            "Reset your LobiLend password",
            "Reset your password",
            "We received a request to reset your LobiLend password. Use the secure link below to choose a new password.",
            "Reset password",
            resetLink,
            "If you did not request this reset, you can ignore this email. Your password will not change.",
            $"Hi {DisplayName(user)},\n\nWe received a request to reset your LobiLend password.\n\nUse this secure link to choose a new password:\n\n{resetLink}\n\nIf you did not request this reset, you can ignore this email. Your password will not change.");
    }

    public static AuthEmailContent UserInvitation(AppUser user, string confirmationLink, string resetLink, IConfiguration configuration)
    {
        return Build(
            configuration,
            "You're invited to LobiLend",
            "You have been invited",
            "You have been invited to join a LobiLend workspace. Confirm your email first, then set your password to finish joining the team.",
            "Confirm email",
            confirmationLink,
            "This invitation was sent by a LobiLend workspace admin.",
            $"Hi {DisplayName(user)},\n\nYou have been invited to join a LobiLend workspace.\n\nFirst confirm your email:\n{confirmationLink}\n\nThen set your password:\n{resetLink}\n\nThis invitation was sent by a LobiLend workspace admin.",
            "Set password",
            resetLink);
    }

    private static AuthEmailContent Build(
        IConfiguration configuration,
        string subject,
        string heading,
        string body,
        string primaryAction,
        string primaryLink,
        string footer,
        string textBody,
        string? secondaryAction = null,
        string? secondaryLink = null)
    {
        var logoUrl = BrandLogoUrl(configuration);
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeBody = WebUtility.HtmlEncode(body);
        var safePrimaryAction = WebUtility.HtmlEncode(primaryAction);
        var safePrimaryLink = WebUtility.HtmlEncode(primaryLink);
        var safeFooter = WebUtility.HtmlEncode(footer);
        var secondaryFallback = string.IsNullOrWhiteSpace(secondaryAction) || string.IsNullOrWhiteSpace(secondaryLink)
            ? string.Empty
            : $"""
              <p style="color: #56636A; font-family: Arial, sans-serif; font-size: 13px; line-height: 20px; margin: 14px 0 10px 0;">Secondary link:</p>
              <p style="color: #28685F; font-family: Arial, sans-serif; font-size: 13px; line-height: 20px; margin: 0; overflow-wrap: anywhere; word-break: break-word;">{WebUtility.HtmlEncode(secondaryLink)}</p>
              """;
        var secondaryButton = string.IsNullOrWhiteSpace(secondaryAction) || string.IsNullOrWhiteSpace(secondaryLink)
            ? string.Empty
            : $"""
              <tr>
                <td style="padding: 0 36px 30px 36px;">
                  <a href="{WebUtility.HtmlEncode(secondaryLink)}" style="display: inline-block; border: 1px solid #D7D0C2; border-radius: 10px; color: #28685F; font-family: Arial, sans-serif; font-size: 15px; font-weight: 700; line-height: 20px; padding: 13px 20px; text-decoration: none;">{WebUtility.HtmlEncode(secondaryAction)}</a>
                </td>
              </tr>
              """;

        var htmlBody = $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{WebUtility.HtmlEncode(subject)}</title>
            </head>
            <body style="margin: 0; padding: 0; background: #F5F0E6;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background: #F5F0E6; border-collapse: collapse; margin: 0; padding: 0; width: 100%;">
                <tr>
                  <td align="center" style="padding: 34px 16px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background: #FBF8F1; border: 1px solid #D7D0C2; border-collapse: separate; border-radius: 18px; box-shadow: 0 18px 44px rgba(52, 56, 47, 0.12); max-width: 620px; overflow: hidden;">
                      <tr>
                        <td style="background: #E8D9B8; border-bottom: 1px solid #D7D0C2; padding: 28px 36px;">
                          <img src="{WebUtility.HtmlEncode(logoUrl)}" alt="LobiLend" width="174" style="border: 0; display: block; height: auto; max-width: 174px;">
                        </td>
                      </tr>
                      <tr>
                        <td style="padding: 36px 36px 14px 36px;">
                          <p style="color: #A9823A; font-family: Arial, sans-serif; font-size: 12px; font-weight: 700; letter-spacing: 0.08em; line-height: 16px; margin: 0 0 12px 0; text-transform: uppercase;">Secure account notice</p>
                          <h1 style="color: #1F292D; font-family: Arial, sans-serif; font-size: 30px; font-weight: 800; line-height: 36px; margin: 0;">{safeHeading}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding: 0 36px 24px 36px;">
                          <p style="color: #56636A; font-family: Arial, sans-serif; font-size: 16px; line-height: 25px; margin: 0;">{safeBody}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding: 0 36px 14px 36px;">
                          <a href="{safePrimaryLink}" style="background: #28685F; border-radius: 10px; color: #FFFFFF; display: inline-block; font-family: Arial, sans-serif; font-size: 15px; font-weight: 700; line-height: 20px; padding: 14px 22px; text-decoration: none;">{safePrimaryAction}</a>
                        </td>
                      </tr>
                      {secondaryButton}
                      <tr>
                        <td style="padding: 8px 36px 32px 36px;">
                          <p style="color: #56636A; font-family: Arial, sans-serif; font-size: 13px; line-height: 20px; margin: 0 0 10px 0;">If the button does not work, copy and paste this link into your browser:</p>
                          <p style="color: #28685F; font-family: Arial, sans-serif; font-size: 13px; line-height: 20px; margin: 0; overflow-wrap: anywhere; word-break: break-word;">{safePrimaryLink}</p>
                          {secondaryFallback}
                        </td>
                      </tr>
                      <tr>
                        <td style="background: #F0ECE4; border-top: 1px solid #D7D0C2; padding: 20px 36px;">
                          <p style="color: #56636A; font-family: Arial, sans-serif; font-size: 12px; line-height: 18px; margin: 0;">{safeFooter}</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return new AuthEmailContent(subject, textBody, htmlBody);
    }

    private static string BrandLogoUrl(IConfiguration configuration)
    {
        var configuredUrl = configuration["Email:Brand:LogoUrl"];

        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl;
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()?.TrimEnd('/')
            ?? "https://lobilend.com";

        return $"{frontendBaseUrl}/LobiLendLogoAndTextTransparent.png";
    }

    private static string DisplayName(AppUser user)
    {
        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? "there" : user.DisplayName;
    }
}

public sealed class DevelopmentAuthEmailSender : IAuthEmailSender
{
    private readonly ILogger<DevelopmentAuthEmailSender> _logger;

    public DevelopmentAuthEmailSender(ILogger<DevelopmentAuthEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Development email confirmation link for {Email}: {Link}", user.Email, confirmationLink);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Development password reset link for {Email}: {Link}", user.Email, resetLink);

        return Task.CompletedTask;
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Development user invitation for {Email}. Confirm: {ConfirmationLink}. Set password: {ResetLink}",
            user.Email,
            confirmationLink,
            resetLink);

        return Task.CompletedTask;
    }
}

public sealed class SmtpAuthEmailSender : IAuthEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpAuthEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.EmailConfirmation(user, confirmationLink, _configuration), cancellationToken);
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.PasswordReset(user, resetLink, _configuration), cancellationToken);
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.UserInvitation(user, confirmationLink, resetLink, _configuration), cancellationToken);
    }

    private async Task SendAsync(string recipient, AuthEmailContent content, CancellationToken cancellationToken)
    {
        var host = Require("Email:Smtp:Host");
        var port = _configuration.GetValue("Email:Smtp:Port", 587);
        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        var fromAddress = Require("Email:Smtp:FromAddress");
        var fromName = _configuration["Email:Smtp:FromName"] ?? "LobiLend";
        var enableSsl = _configuration.GetValue("Email:Smtp:EnableSsl", true);
        var messageStream = _configuration["Email:Smtp:MessageStream"];

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = content.Subject,
            Body = content.TextBody
        };
        message.To.Add(recipient);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(content.TextBody, Encoding.UTF8, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(content.HtmlBody, Encoding.UTF8, "text/html"));

        if (!string.IsNullOrWhiteSpace(messageStream))
        {
            message.Headers.Add("X-PM-Message-Stream", messageStream);
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private string Require(string key)
    {
        var value = _configuration[key];

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} must be configured for SMTP email.")
            : value;
    }
}

public sealed class MailgunAuthEmailSender : IAuthEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MailgunAuthEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.EmailConfirmation(user, confirmationLink, _configuration), cancellationToken);
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.PasswordReset(user, resetLink, _configuration), cancellationToken);
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(user.Email ?? string.Empty, AuthEmailTemplates.UserInvitation(user, confirmationLink, resetLink, _configuration), cancellationToken);
    }

    private async Task SendAsync(string recipient, AuthEmailContent content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new InvalidOperationException("Mailgun recipient email is required.");
        }

        var apiKey = Require("Email:Mailgun:ApiKey");
        var domain = Require("Email:Mailgun:Domain");
        var baseUrl = (_configuration["Email:Mailgun:BaseUrl"] ?? "https://api.mailgun.net").TrimEnd('/');
        var fromAddress = _configuration["Email:Mailgun:FromAddress"] ?? $"postmaster@{domain}";
        var fromName = _configuration["Email:Mailgun:FromName"] ?? "LobiLend";
        var requestUri = $"{baseUrl}/v3/{Uri.EscapeDataString(domain)}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}")));

        using var form = new MultipartFormDataContent
        {
            { new StringContent($"{fromName} <{fromAddress}>"), "from" },
            { new StringContent(recipient), "to" },
            { new StringContent(content.Subject), "subject" },
            { new StringContent(content.TextBody), "text" },
            { new StringContent(content.HtmlBody), "html" }
        };
        request.Content = form;

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Mailgun email send failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }
    }

    private string Require(string key)
    {
        var value = _configuration[key];

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} must be configured for Mailgun email.")
            : value;
    }
}

public sealed class MissingProductionAuthEmailSender : IAuthEmailSender
{
    public Task SendEmailConfirmationAsync(AppUser user, string confirmationLink, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Production email delivery is not configured.");
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Production email delivery is not configured.");
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Production email delivery is not configured.");
    }
}
