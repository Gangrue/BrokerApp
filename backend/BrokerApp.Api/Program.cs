using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Customers;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Intake;
using BrokerApp.Api.Features.Loans;
using BrokerApp.Api.Features.Reports;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<BrokerAppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("BrokerApp")));
}
builder.Services.AddControllers();
builder.Services.AddScoped<IActionWorkflowService, ActionWorkflowService>();
builder.Services.AddScoped<IActionPublicIdGenerator, ActionPublicIdGenerator>();
builder.Services.AddScoped<IActionTemplateService, ActionTemplateService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IIntakeService, IntakeService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

var app = builder.Build();

if (app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") && !EF.IsDesignTime)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerAppDbContext>();

    await dbContext.Database.MigrateAsync();
    await DevDataSeeder.SeedAsync(dbContext);
}

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
