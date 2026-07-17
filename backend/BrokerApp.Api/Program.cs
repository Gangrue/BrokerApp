using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Loans;
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
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ILoanService, LoanService>();
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
