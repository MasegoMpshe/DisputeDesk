using System.Security.Claims;
using DisputeDesk.Application.Dashboard;
using DisputeDesk.Application.Disputes;
using DisputeDesk.Api;
using DisputeDesk.Application.Transactions;
using DisputeDesk.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DisputeDeskDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DisputeDesk");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Configure the DisputeDesk database connection string.");
    }

    options.UseNpgsql(connectionString);
    if (builder.Configuration.GetValue<bool>("Database:SeedDemoData"))
    {
        options.UseDemoData();
    }
});

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DisputeDeskDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-DisputeDesk";
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(DisputeAccess.Customer, policy => policy.RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.NameIdentifier)
        .RequireAssertion(context => !context.User.IsInRole(DisputeAccess.Reviewer)))
    .AddPolicy(DisputeAccess.Reviewer, policy => policy.RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.NameIdentifier)
        .RequireRole(DisputeAccess.Reviewer));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ProvisionDemoAccounts>();
builder.Services.AddScoped<ITransactionReader, TransactionReader>();
builder.Services.AddScoped<ICustomerDashboardReader, CustomerDashboardReader>();
builder.Services.AddScoped<DisputeReader>();
builder.Services.AddScoped<IDisputeReader>(services => services.GetRequiredService<DisputeReader>());
builder.Services.AddScoped<IReviewerDisputeReader>(services => services.GetRequiredService<DisputeReader>());
builder.Services.AddScoped<IDisputeStore, DisputeStore>();
builder.Services.AddScoped<DisputeWorkflow>();
builder.Services.AddScoped<IDisputeWriter>(services => services.GetRequiredService<DisputeWorkflow>());
builder.Services.AddScoped<IDisputeNotifications, DisputeNotificationStore>();
builder.Services.Configure<DemoTransactionOptions>(builder.Configuration.GetSection(DemoTransactionOptions.SectionName));
builder.Services.AddScoped<IDemoTransactionGenerator, DemoTransactionGenerator>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

var migrateDatabase = app.Configuration.GetValue<bool>("Database:Migrate");
var provisionDemoAccounts = app.Configuration.GetValue<bool>("Database:ProvisionDemoAccounts");

if (migrateDatabase || provisionDemoAccounts)
{
    await using (app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        if (migrateDatabase)
        {
            var database = scope.ServiceProvider.GetRequiredService<DisputeDeskDbContext>();
            await database.Database.MigrateAsync();
        }

        if (provisionDemoAccounts)
        {
            var command = scope.ServiceProvider.GetRequiredService<ProvisionDemoAccounts>();
            await command.ExecuteAsync();
        }
    }

    return;
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "DisputeDesk API v1");
        options.DocumentTitle = "DisputeDesk API";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
