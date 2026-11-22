using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using DisputeDesk.Application.Transactions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DisputeDesk.Tests;

public sealed class TransactionsAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TransactionSummary NalediTransaction =
        new(Guid.Parse("20000000-0000-4000-8000-000000000001"), "Grocery", 32.50m, "ZAR", DateTimeOffset.UnixEpoch);

    private static readonly TransactionSummary ThaboTransaction =
        new(Guid.Parse("20000000-0000-4000-8000-000000000003"), "Fuel", 60m, "ZAR", DateTimeOffset.UnixEpoch);

    private readonly WebApplicationFactory<Program> factory;

    public TransactionsAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<ISecurityStampValidator, TestSecurityStampValidator>());
                services.Replace(ServiceDescriptor.Scoped<ITransactionReader, TestTransactionReader>());
            });
        });
    }

    [Fact]
    public async Task AnonymousRequestsReturnUnauthorized()
    {
        using var client = CreateClient();

        using var list = await client.GetAsync("/api/transactions");
        using var detail = await client.GetAsync($"/api/transactions/{NalediTransaction.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    [Fact]
    public async Task ListUsesTheSignedInIdentity()
    {
        using var client = CreateClient("demo-naledi");

        var transactions = await client.GetFromJsonAsync<TransactionSummary[]>(
            "/api/transactions?identityUserId=demo-thabo");

        Assert.Equal(NalediTransaction, Assert.Single(transactions!));
    }

    [Fact]
    public async Task DetailHidesTransactionsOwnedByAnotherCustomer()
    {
        using var client = CreateClient("demo-naledi");

        var owned = await client.GetFromJsonAsync<TransactionSummary>($"/api/transactions/{NalediTransaction.Id}");
        using var other = await client.GetAsync($"/api/transactions/{ThaboTransaction.Id}");

        Assert.Equal(NalediTransaction, owned);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    private HttpClient CreateClient(string? identityUserId = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        if (identityUserId is not null)
        {
            var scheme = IdentityConstants.ApplicationScheme;
            var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, identityUserId)], scheme);
            var properties = new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5)
            };
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, scheme);
            client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        }

        return client;
    }

    private sealed class TestSecurityStampValidator : ISecurityStampValidator
    {
        public Task ValidateAsync(CookieValidatePrincipalContext context) => Task.CompletedTask;
    }

    private sealed class TestTransactionReader : ITransactionReader
    {
        public Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
            Task.FromResult(identityUserId == "demo-naledi" ? new[] { NalediTransaction } : new[] { ThaboTransaction });

        public async Task<TransactionSummary?> FindAsync(
            string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
            (await ListAsync(identityUserId, cancellationToken)).SingleOrDefault(transaction => transaction.Id == transactionId);
    }
}
