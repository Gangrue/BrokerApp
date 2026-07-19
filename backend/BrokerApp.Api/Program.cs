using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Customers;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Intake;
using BrokerApp.Api.Features.Loans;
using BrokerApp.Api.Features.Reports;
using BrokerApp.Api.Features.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<BrokerAppDbContext>(options =>
    {
        options.UseNpgsql(NormalizePostgresConnectionString(builder.Configuration.GetConnectionString("BrokerApp")));
        options.UseOpenIddict<Guid>();
    });
}
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://127.0.0.1:5173", "http://localhost:5173"];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "BrokerApp.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services
    .AddIdentity<AppUser, IdentityRole<Guid>>(options =>
    {
        options.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue("Auth:RequireConfirmedEmail", true);
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<BrokerAppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "BrokerApp.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    var cookieDomain = builder.Configuration["Auth:CookieDomain"];
    if (!string.IsNullOrWhiteSpace(cookieDomain))
    {
        options.Cookie.Domain = cookieDomain;
    }

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, BrokerUserClaimsPrincipalFactory>();
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<BrokerAppDbContext>()
            .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetEndSessionEndpointUris("/connect/logout");
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow();
        options.RegisterScopes(
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles);

        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
            options.UseAspNetCore()
                .DisableTransportSecurityRequirement();
        }
        else
        {
            options.AddEncryptionCertificate(LoadCertificate(
                builder.Configuration,
                "Auth:OpenIddict:EncryptionCertificatePath",
                "Auth:OpenIddict:EncryptionCertificatePassword"));
            options.AddSigningCertificate(LoadCertificate(
                builder.Configuration,
                "Auth:OpenIddict:SigningCertificatePath",
                "Auth:OpenIddict:SigningCertificatePassword"));
            options.UseAspNetCore();
        }
    });
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IAuthEmailSender>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    if (string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
    {
        return serviceProvider.GetRequiredService<SmtpAuthEmailSender>();
    }

    if (string.Equals(configuration["Email:Provider"], "Mailgun", StringComparison.OrdinalIgnoreCase))
    {
        return serviceProvider.GetRequiredService<MailgunAuthEmailSender>();
    }

    return environment.IsDevelopment()
        ? serviceProvider.GetRequiredService<DevelopmentAuthEmailSender>()
        : serviceProvider.GetRequiredService<MissingProductionAuthEmailSender>();
});
builder.Services.AddScoped<DevelopmentAuthEmailSender>();
builder.Services.AddScoped<SmtpAuthEmailSender>();
builder.Services.AddHttpClient<MailgunAuthEmailSender>();
builder.Services.AddScoped<MissingProductionAuthEmailSender>();
builder.Services.AddScoped<IActionWorkflowService, ActionWorkflowService>();
builder.Services.AddScoped<IActionPublicIdGenerator, ActionPublicIdGenerator>();
builder.Services.AddScoped<IActionTemplateService, ActionTemplateService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IIntakeService, IntakeService>();
builder.Services.AddScoped<ILoanFileCreationService, LoanFileCreationService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

var app = builder.Build();

if (app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") && !EF.IsDesignTime)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerAppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    await dbContext.Database.MigrateAsync();
    await DevDataSeeder.SeedAsync(dbContext, userManager, applicationManager);
}

app.UseForwardedHeaders();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

static X509Certificate2 LoadCertificate(IConfiguration configuration, string pathKey, string passwordKey)
{
    var base64Key = pathKey.Replace("Path", "Base64", StringComparison.OrdinalIgnoreCase);
    var base64 = configuration[base64Key];
    var password = configuration[passwordKey];

    if (!string.IsNullOrWhiteSpace(base64))
    {
        return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64), password);
    }

    var path = configuration[pathKey];

    if (string.IsNullOrWhiteSpace(path))
    {
        throw new InvalidOperationException($"{pathKey} or {base64Key} must be configured in Production.");
    }

    return X509CertificateLoader.LoadPkcs12FromFile(path, password);
}

static string NormalizePostgresConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:BrokerApp must be configured.");
    }

    if (!connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
        && !connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
    };

    foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = parameter.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

        if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = Enum.Parse<SslMode>(value, ignoreCase: true);
        }
    }

    return builder.ConnectionString;
}

public partial class Program;
