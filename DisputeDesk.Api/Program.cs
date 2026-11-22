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
builder.Services.AddAuthorization();
builder.Services.AddScoped<ProvisionDemoAccounts>();
builder.Services.AddScoped<ITransactionReader, TransactionReader>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ProvisionDemoAccounts"))
{
    await using (app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var command = scope.ServiceProvider.GetRequiredService<ProvisionDemoAccounts>();
        await command.ExecuteAsync();
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
