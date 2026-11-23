using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using DisputeDesk.Application.Dashboard;
using DisputeDesk.Application.Disputes;
using DisputeDesk.Application.Transactions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    private static readonly DisputeSummary NalediDispute =
        new(Guid.Parse("30000000-0000-4000-8000-000000000001"), NalediTransaction.Id,
            "Grocery", "DD-001", "Submitted", DateTimeOffset.UnixEpoch);

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
                services.Replace(ServiceDescriptor.Scoped<IDemoTransactionGenerator, DisabledDemoGenerator>());
                services.Replace(ServiceDescriptor.Scoped<ICustomerDashboardReader, TestCustomerReader>());
                services.Replace(ServiceDescriptor.Scoped<IDisputeReader, TestCustomerReader>());
                services.Replace(ServiceDescriptor.Scoped<IDisputeWriter, TestDisputeWriter>());
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
        Assert.Equal("application/problem+json", list.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (await list.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
        using var dashboard = await client.GetAsync("/api/dashboard");
        using var disputes = await client.GetAsync("/api/disputes");
        Assert.Equal(HttpStatusCode.Unauthorized, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, disputes.StatusCode);
    }

    [Fact]
    public async Task ApiErrorsUseProblemDetailsWithoutExposingExceptionDetails()
    {
        using var client = CreateClient("test-backend-failure");
        using var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
        Assert.DoesNotContain("Internal test marker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthenticatedRequestsWithBlankIdentityAreForbidden()
    {
        using var client = CreateClient(" ");
        using var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsesTheSignedInIdentity()
    {
        using var client = CreateClient("demo-naledi");

        var transactions = await client.GetFromJsonAsync<TransactionSummary[]>(
            "/api/transactions?identityUserId=demo-thabo");

        Assert.Equal(NalediTransaction with { FilingWindowExpired = true }, Assert.Single(transactions!));
    }

    [Fact]
    public async Task DetailHidesTransactionsOwnedByAnotherCustomer()
    {
        using var client = CreateClient("demo-naledi");

        var owned = await client.GetFromJsonAsync<TransactionSummary>($"/api/transactions/{NalediTransaction.Id}");
        using var other = await client.GetAsync($"/api/transactions/{ThaboTransaction.Id}");

        Assert.Equal(NalediTransaction with { FilingWindowExpired = true }, owned);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task DashboardUsesTheSignedInIdentity()
    {
        using var client = CreateClient("demo-naledi");

        var summary = Assert.IsType<CustomerDashboard>(
            await client.GetFromJsonAsync<CustomerDashboard>("/api/dashboard?identityUserId=demo-thabo"));

        Assert.Equal(7, summary.TotalTransactionCount);
        Assert.Equal(1, summary.TotalDisputes);
        Assert.Equal(NalediTransaction with { FilingWindowExpired = true }, Assert.Single(summary.RecentTransactions));
    }

    [Fact]
    public async Task DisputesUseTheSignedInIdentity()
    {
        using var owner = CreateClient("demo-naledi");
        using var other = CreateClient("demo-thabo");

        var owned = await owner.GetFromJsonAsync<DisputeSummary[]>("/api/disputes");
        var excluded = await other.GetFromJsonAsync<DisputeSummary[]>("/api/disputes?identityUserId=demo-naledi");

        Assert.Equal(NalediTransaction.Id, Assert.Single(owned!).TransactionId);
        Assert.Empty(excluded!);
    }

    [Fact]
    public async Task DisputeSubmissionReturnsCreatedWithReceiptLocation()
    {
        using var owner = CreateClient("demo-naledi");

        using var response = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "  UNRECOGNISED  ",
            details = "  Check the receipt.  "
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = Assert.IsType<DisputeDetails>(await response.Content.ReadFromJsonAsync<DisputeDetails>());
        Assert.Equal("/api/disputes/" + result.Summary.Id, response.Headers.Location?.AbsolutePath);
        Assert.Equal("Unrecognised purchase", result.Reason);
        Assert.Equal("Check the receipt.", result.Details);
    }

    [Fact]
    public async Task DisputeSubmissionRejectsAnonymousInvalidAndForeignRequests()
    {
        using var anonymous = CreateClient();
        using var owner = CreateClient("demo-naledi");

        using var unsigned = await anonymous.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "UNRECOGNISED"
        });
        using var invalid = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "   "
        });
        using var foreign = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = ThaboTransaction.Id,
            reasonCode = "UNRECOGNISED"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task DisputeSubmissionReturnsConflictForDuplicate()
    {
        using var owner = CreateClient("demo-naledi");

        using var response = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "DUPLICATE"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptUsesTheSignedInIdentity()
    {
        using var owner = CreateClient("demo-naledi");
        using var other = CreateClient("demo-thabo");
        var url = $"/api/disputes/{NalediDispute.Id}";

        var receipt = Assert.IsType<DisputeDetails>(await owner.GetFromJsonAsync<DisputeDetails>(url));
        using var excluded = await other.GetAsync(url + "?identityUserId=demo-naledi");

        Assert.Equal(NalediDispute, receipt.Summary);
        Assert.Equal(HttpStatusCode.NotFound, excluded.StatusCode);
    }

    [Fact]
    public async Task DisputeReasonLookupAndRequiredDetailsUseValidationResponses()
    {
        using var owner = CreateClient("demo-naledi");
        var reasons = await owner.GetFromJsonAsync<DisputeReasonOption[]>("/api/disputes/reasons");
        Assert.Equal("UNRECOGNISED", Assert.Single(reasons!).Code);

        using var invalid = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "UNKNOWN_CODE"
        });
        using var missing = await owner.PostAsJsonAsync("/api/disputes", new
        {
            transactionId = NalediTransaction.Id,
            reasonCode = "OTHER"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task DemoGenerationIsHiddenWhenDisabled()
    {
        using var owner = CreateClient("demo-naledi");
        var dashboard = await owner.GetFromJsonAsync<CustomerDashboard>("/api/dashboard");
        using var response = await owner.PostAsJsonAsync("/api/transactions/demo", new { });

        Assert.False(dashboard!.CanGenerateTransactions);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TransactionPagingRejectsInvalidFilters()
    {
        using var owner = CreateClient("demo-naledi");
        using var invalidRange = await owner.GetAsync("/api/transactions/page?from=2026-11-23&to=2026-11-22");
        using var invalidSize = await owner.GetAsync("/api/transactions/page?pageSize=51");
        using var invalidSort = await owner.GetAsync("/api/transactions/page?sort=99");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSize.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);
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

    private sealed class TestCustomerReader : ICustomerDashboardReader, IDisputeReader
    {
        public Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<DisputeReasonOption[]>([new("UNRECOGNISED", "Unrecognised purchase", false)]);
        public Task<CustomerDashboard> GetAsync(string identityUserId, CancellationToken cancellationToken) =>
            Task.FromResult(identityUserId == "demo-naledi"
                ? new CustomerDashboard(7, 1, [NalediTransaction])
                : new CustomerDashboard(3, 0, [ThaboTransaction]));

        public Task<DisputeSummary[]> ListAsync(string identityUserId, DisputeFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult<DisputeSummary[]>(identityUserId == "demo-naledi"
                ? [NalediDispute]
                : []);

        public Task<DisputeDetails?> FindAsync(string identityUserId, Guid disputeId, CancellationToken cancellationToken) =>
            Task.FromResult<DisputeDetails?>(identityUserId == "demo-naledi" && disputeId == NalediDispute.Id
                ? new DisputeDetails(NalediDispute, "Unrecognised purchase", null)
                : null);
    }

    private sealed class TestDisputeWriter : IDisputeWriter
    {
        public Task<DisputeSubmissionResult> SubmitAsync(
            string identityUserId, Guid transactionId, string reasonCode, string? details, CancellationToken cancellationToken)
        {
            DisputeSubmissionResult result = (identityUserId, transactionId, reasonCode) switch
            {
                ("demo-naledi", var id, "DUPLICATE") when id == NalediTransaction.Id =>
                    new DisputeSubmissionResult.Duplicate(),
                ("demo-naledi", var id, "UNRECOGNISED") when id == NalediTransaction.Id =>
                    new DisputeSubmissionResult.Created(new DisputeDetails(NalediDispute, "Unrecognised purchase", details)),
                ("demo-naledi", var id, "OTHER") when id == NalediTransaction.Id && string.IsNullOrWhiteSpace(details) =>
                    new DisputeSubmissionResult.DetailsRequired(),
                ("demo-naledi", var id, _) when id == NalediTransaction.Id =>
                    new DisputeSubmissionResult.InvalidReason(),
                _ => new DisputeSubmissionResult.TransactionNotFound()
            };
            return Task.FromResult(result);
        }
    }

    private sealed class DisabledDemoGenerator : IDemoTransactionGenerator
    {
        public Task<int> GenerateAsync(string identityUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A disabled demo generator must not be called.");
    }

    private sealed class TestTransactionReader : ITransactionReader
    {
        public Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
            identityUserId == "test-backend-failure"
                ? throw new InvalidOperationException("Internal test marker")
                : Task.FromResult(identityUserId == "demo-naledi" ? new[] { NalediTransaction } : new[] { ThaboTransaction });

        public async Task<TransactionPage> PageAsync(string identityUserId, TransactionFilter filter,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            var items = await ListAsync(identityUserId, cancellationToken);
            return new TransactionPage(items.Select(item => item.WithFilingWindowAt(now)).ToArray(),
                items.Length, 1, filter.PageSize);
        }

        public async Task<TransactionSummary?> FindAsync(
            string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
            (await ListAsync(identityUserId, cancellationToken)).SingleOrDefault(transaction => transaction.Id == transactionId);
    }
}
