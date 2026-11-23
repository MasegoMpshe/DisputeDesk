using DisputeDesk.Domain;
using DisputeDesk.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace DisputeDesk.Tests;

public sealed class PostgreSqlDisputeFixture : IAsyncLifetime
{
    public const string AdminConnectionVariable = "DISPUTEDESK_TEST_POSTGRES";
    public const string CustomerUser = "test-customer";
    public const string OtherUser = "test-other";
    public const string ReviewerUser = "test-reviewer";
    public const string Password = "Local-Integration1!";

    public static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid BoundaryTransaction = Guid.NewGuid();
    public static readonly Guid OldTransaction = Guid.NewGuid();
    public static readonly Guid OtherTransaction = Guid.NewGuid();
    public static readonly Guid RaceTransaction = Guid.NewGuid();
    public static readonly Guid WithdrawalTransaction = Guid.NewGuid();
    public static readonly Guid LegacyTransaction = Guid.NewGuid();
    public static readonly Guid LegacyDispute = Guid.NewGuid();

    private readonly string databaseName = $"disputedesk_test_{Guid.NewGuid():N}";
    private NpgsqlConnection? admin;
    private bool created;
    public WebApplicationFactory<Program> App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(AdminConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        try
        {
            admin = new NpgsqlConnection(connection.ConnectionString);
            await admin.OpenAsync();
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            {
                await create.ExecuteNonQueryAsync();
                created = true;
            }

            connection.Database = databaseName;
            App = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, options) => options.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DisputeDesk"] = connection.ConnectionString,
                        ["DemoTransactions:Enabled"] = "true"
                    }));
                builder.ConfigureTestServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedTimeProvider())));
            });

            await using var scope = App.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DisputeDeskDbContext>();
            var migrator = database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261122165114_AddDisputeReasonsAndMerchantDetails");

            var customer = new Customer { Id = Guid.NewGuid(), IdentityUserId = CustomerUser, DisplayName = "Customer" };
            var other = new Customer { Id = Guid.NewGuid(), IdentityUserId = OtherUser, DisplayName = "Other" };
            database.AddRange(customer, other);
            database.AddRange(
                Purchase(BoundaryTransaction, customer.Id, Now.AddDays(-120)),
                Purchase(OldTransaction, customer.Id, Now.AddDays(-120).AddSeconds(-1)),
                Purchase(OtherTransaction, other.Id, Now),
                Purchase(RaceTransaction, customer.Id, Now),
                Purchase(WithdrawalTransaction, customer.Id, Now),
                Purchase(LegacyTransaction, other.Id, Now.AddDays(-200)));
            await database.SaveChangesAsync();
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO disputes (id, transaction_id, reference_number, reason, status, submitted_at)
                VALUES ({LegacyDispute}, {LegacyTransaction}, 'DD-LEGACY', 'Original reason', 'Submitted', {Now.AddDays(-180)})
                """);
            await migrator.MigrateAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Reviewer"))).Succeeded);
            foreach (var id in new[] { CustomerUser, OtherUser, ReviewerUser })
            {
                var user = new IdentityUser { Id = id, UserName = id, LockoutEnabled = true };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                if (id == ReviewerUser)
                {
                    Assert.True((await users.AddToRoleAsync(user, "Reviewer")).Succeeded);
                }
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
            App = null!;
        }

        if (admin is not null)
        {
            await using (admin)
            {
                if (created)
                {
                    await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
                    await drop.ExecuteNonQueryAsync();
                    created = false;
                }
            }
            admin = null;
        }
    }

    private static Transaction Purchase(Guid id, Guid customerId, DateTimeOffset date) => new()
    {
        Id = id, CustomerId = customerId, Description = "Test purchase",
        Amount = 42.50m, Currency = "ZAR", OccurredAt = date
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlDisputeFixture.AdminConnectionVariable)))
        {
            Skip = $"Set {PostgreSqlDisputeFixture.AdminConnectionVariable} to run PostgreSQL integration tests.";
        }
    }
}
