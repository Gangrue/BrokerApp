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
        return SendAsync(
            user.Email ?? string.Empty,
            "Confirm your LobiLend email",
            $"Confirm your email to finish setting up LobiLend:\n\n{confirmationLink}",
            cancellationToken);
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(
            user.Email ?? string.Empty,
            "Reset your LobiLend password",
            $"Reset your LobiLend password here:\n\n{resetLink}",
            cancellationToken);
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        var body = new StringBuilder()
            .AppendLine($"Hi {user.DisplayName},")
            .AppendLine()
            .AppendLine("You have been invited to LobiLend.")
            .AppendLine()
            .AppendLine("Confirm your email:")
            .AppendLine(confirmationLink)
            .AppendLine()
            .AppendLine("Then set your password:")
            .AppendLine(resetLink)
            .ToString();

        return SendAsync(user.Email ?? string.Empty, "You're invited to LobiLend", body, cancellationToken);
    }

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
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
            Subject = subject,
            Body = body
        };
        message.To.Add(recipient);

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
        return SendAsync(
            user.Email ?? string.Empty,
            "Confirm your LobiLend email",
            $"Confirm your email to finish setting up LobiLend:\n\n{confirmationLink}",
            cancellationToken);
    }

    public Task SendPasswordResetAsync(AppUser user, string resetLink, CancellationToken cancellationToken)
    {
        return SendAsync(
            user.Email ?? string.Empty,
            "Reset your LobiLend password",
            $"Reset your LobiLend password here:\n\n{resetLink}",
            cancellationToken);
    }

    public Task SendUserInvitationAsync(AppUser user, string confirmationLink, string resetLink, CancellationToken cancellationToken)
    {
        var body = new StringBuilder()
            .AppendLine($"Hi {user.DisplayName},")
            .AppendLine()
            .AppendLine("You have been invited to LobiLend.")
            .AppendLine()
            .AppendLine("Confirm your email:")
            .AppendLine(confirmationLink)
            .AppendLine()
            .AppendLine("Then set your password:")
            .AppendLine(resetLink)
            .ToString();

        return SendAsync(user.Email ?? string.Empty, "You're invited to LobiLend", body, cancellationToken);
    }

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
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
            { new StringContent(subject), "subject" },
            { new StringContent(body), "text" }
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
