using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DisputeDesk.Api;
using DisputeDesk.Application.Dashboard;
using DisputeDesk.Application.Disputes;
using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DisputeDesk.Infrastructure;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using static DisputeDesk.Tests.PostgreSqlDisputeFixture;

namespace DisputeDesk.Tests;

public sealed class DisputeLifecycleIntegrationTests(PostgreSqlDisputeFixture fixture)
    : IClassFixture<PostgreSqlDisputeFixture>
{
    [PostgreSqlFact]
    public async Task SetupRecoversAnExistingLockedDemoReviewerUsingTheConfiguredPassword()
    {
        const string reviewerId = "demo-reviewer";
        const string oldPassword = "Old-Demo1!";
        const string configuredPassword = "Configured-Demo2!";
        using var setupApp = fixture.App.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DemoAccounts:demo-naledi:Password"] = configuredPassword,
                    ["DemoAccounts:demo-thabo:Password"] = configuredPassword,
                    ["DemoAccounts:demo-reviewer:Password"] = configuredPassword
                })));

        await using (var scope = setupApp.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DisputeDeskDbContext>();
            database.Customers.AddRange(
                new Customer { Id = Guid.NewGuid(), IdentityUserId = "demo-naledi", DisplayName = "Naledi" },
                new Customer { Id = Guid.NewGuid(), IdentityUserId = "demo-thabo", DisplayName = "Thabo" });
            await database.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var reviewer = new IdentityUser { Id = reviewerId, UserName = reviewerId, LockoutEnabled = true };
            Assert.True((await users.CreateAsync(reviewer, oldPassword)).Succeeded);
            Assert.True((await users.AccessFailedAsync(reviewer)).Succeeded);
            Assert.True((await users.SetLockoutEndDateAsync(reviewer, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
            var existingCustomer = new IdentityUser { Id = "demo-naledi", UserName = "demo-naledi" };
            Assert.True((await users.CreateAsync(existingCustomer, oldPassword)).Succeeded);

            var setup = scope.ServiceProvider.GetRequiredService<ProvisionDemoAccounts>();
            await setup.ExecuteAsync();
            var stamp = await users.GetSecurityStampAsync(reviewer);
            await setup.ExecuteAsync();

            Assert.False(await users.IsLockedOutAsync(reviewer));
            Assert.Equal(0, await users.GetAccessFailedCountAsync(reviewer));
            Assert.Equal(stamp, await users.GetSecurityStampAsync(reviewer));
            Assert.True(await users.CheckPasswordAsync(existingCustomer, oldPassword));
        }

        using var client = setupApp.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { userName = reviewerId, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName = reviewerId, password = configuredPassword });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var queue = await client.GetAsync("/api/reviewer/disputes");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
    }

    [PostgreSqlFact]
    public async Task ReadinessSucceedsWithMigratedPostgreSql()
    {
        using var client = fixture.App.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgreSqlFact]
    public async Task StructuredRequestMetadataIsPersistedWithTheResponseCorrelationId()
    {
        using var loggedApp = fixture.App.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RequestLogging:PersistToPostgreSql"] = "true" })));
        using var client = loggedApp.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/api/disputes/page?search=private-query-value");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));

        await using var scope = fixture.App.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DisputeDeskDbContext>();
        await using var connection = new NpgsqlConnection(database.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("""
            SELECT method, route_pattern, status_code, duration_ms, level
            FROM api_request_logs WHERE correlation_id = $1
            """, connection);
        query.Parameters.Add(new NpgsqlParameter<string> { TypedValue = correlationId });
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using (var row = await query.ExecuteReaderAsync(wait.Token))
            {
                if (await row.ReadAsync(wait.Token))
                {
                    Assert.Equal("GET", row.GetString(0));
                    Assert.Equal("api/disputes/page", row.GetString(1).TrimStart('/'));
                    Assert.Equal(401, row.GetInt32(2));
                    Assert.True(row.GetDouble(3) >= 0);
                    Assert.Equal("Information", row.GetString(4));
                    return;
                }
            }

            await Task.Delay(250, wait.Token);
        }
    }

    [PostgreSqlFact]
    public async Task ReviewerDecisionUpdatesTheCustomerTimelineAndNotifications()
    {
        using var customer = await SignInAsync(CustomerUser);
        using var reviewer = await SignInAsync(ReviewerUser);
        using var other = await SignInAsync(OtherUser);
        var session = await reviewer.GetFromJsonAsync<JsonElement>("/api/auth/session");
        Assert.Equal("Reviewer", session.GetProperty("role").GetString());
        Assert.Equal(ReviewerUser, session.GetProperty("displayName").GetString());
        var customerSession = await customer.GetFromJsonAsync<JsonElement>("/api/auth/session");
        Assert.Equal("Customer", customerSession.GetProperty("displayName").GetString());

        var transactions = await customer.GetFromJsonAsync<TransactionSummary[]>("/api/transactions");
        var boundary = Assert.Single(transactions!, item => item.Id == BoundaryTransaction);
        Assert.Equal(Now, boundary.DisputeDeadline);
        Assert.False(boundary.FilingWindowExpired);

        var expired = await customer.GetFromJsonAsync<TransactionSummary>($"/api/transactions/{OldTransaction}");
        Assert.True(expired!.FilingWindowExpired);
        Assert.Equal(Now.AddSeconds(-1), expired.DisputeDeadline);
        Assert.Equal(expired, Assert.Single(transactions!, item => item.Id == OldTransaction));
        using var late = await SubmitAsync(customer, OldTransaction);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        var error = await late.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("filingWindowExpired", error.GetProperty("code").GetString());
        Assert.Contains("120 days", error.GetProperty("errors").GetProperty("TransactionId")[0].GetString());
        using var foreign = await SubmitAsync(customer, OtherTransaction);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        using var submitted = await SubmitAsync(customer, BoundaryTransaction);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var dispute = Assert.IsType<DisputeDetails>(await submitted.Content.ReadFromJsonAsync<DisputeDetails>());
        Assert.Matches("^DD-[0-9]{8}$", dispute.Summary.ReferenceNumber);
        Assert.Equal("Submitted", Assert.Single(dispute.Timeline).Status);
        var id = dispute.Summary.Id;

        using var forbidden = await customer.GetAsync("/api/reviewer/disputes");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenReview = await customer.PostAsJsonAsync($"/api/reviewer/disputes/{id}/review", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReview.StatusCode);
        using var reviewerCustomerRoute = await reviewer.GetAsync("/api/disputes");
        Assert.Equal(HttpStatusCode.Forbidden, reviewerCustomerRoute.StatusCode);

        var queue = await reviewer.GetFromJsonAsync<DisputeSummary[]>("/api/reviewer/disputes");
        Assert.Contains(queue!, item => item.Id == id);
        using var premature = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{id}/decision",
            new { status = "Approved", note = "Reviewed" });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var foreignWithdraw = await other.PostAsJsonAsync($"/api/disputes/{id}/withdraw", new { });
        Assert.Equal(HttpStatusCode.NotFound, foreignWithdraw.StatusCode);

        using var review = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{id}/review", new { });
        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        using var deniedWithdrawal = await customer.PostAsJsonAsync($"/api/disputes/{id}/withdraw", new { });
        Assert.Equal(HttpStatusCode.Conflict, deniedWithdrawal.StatusCode);
        using var missingNote = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{id}/decision",
            new { status = "Approved", note = " " });
        Assert.Equal(HttpStatusCode.BadRequest, missingNote.StatusCode);
        using var decision = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{id}/decision",
            new { status = "Approved", note = "Customer claim upheld." });
        Assert.Equal(HttpStatusCode.NoContent, decision.StatusCode);

        var receipt = await customer.GetFromJsonAsync<DisputeDetails>($"/api/disputes/{id}");
        Assert.Equal(new[] { "Submitted", "Under review", "Approved" }, receipt!.Timeline.Select(item => item.Status));
        Assert.Equal("Customer claim upheld.", receipt.Timeline.Last().Note);
        Assert.Equal(42.50m, receipt.Summary.Amount);
        var dashboard = (await customer.GetFromJsonAsync<CustomerDashboard>("/api/dashboard"))!;
        Assert.Equal(Enum.GetValues<DisputeStatus>().Length, dashboard.DisputesByStatus.Count);
        Assert.Equal(1, dashboard.DisputesByStatus[DisputeStatus.Approved]);
        Assert.Equal(0, dashboard.DisputesByStatus[DisputeStatus.UnderReview]);
        Assert.Equal(dashboard.TotalDisputes, dashboard.DisputesByStatus.Values.Sum());
        var otherDashboard = (await other.GetFromJsonAsync<CustomerDashboard>("/api/dashboard"))!;
        Assert.Equal(0, otherDashboard.DisputesByStatus[DisputeStatus.Approved]);

        var matching = await customer.GetFromJsonAsync<DisputeSummary[]>("/api/disputes?status=Approved&from=2026-11-23");
        Assert.Equal(id, Assert.Single(matching!).Id);
        Assert.Empty((await customer.GetFromJsonAsync<DisputeSummary[]>("/api/disputes?status=Rejected"))!);
        Assert.Empty((await customer.GetFromJsonAsync<DisputeSummary[]>("/api/disputes?from=2026-11-24"))!);

        using var paginatedSubmission = await SubmitAsync(customer, PaginationTransaction);
        Assert.Equal(HttpStatusCode.Created, paginatedSubmission.StatusCode);
        var secondReceipt = Assert.IsType<DisputeDetails>(await paginatedSubmission.Content.ReadFromJsonAsync<DisputeDetails>());
        Assert.Matches("^DD-[0-9]{8}$", secondReceipt.Summary.ReferenceNumber);
        Assert.NotEqual(dispute.Summary.ReferenceNumber, secondReceipt.Summary.ReferenceNumber);

        var first = Assert.IsType<DisputePage>(
            await customer.GetFromJsonAsync<DisputePage>("/api/disputes/page?from=2026-11-23&pageSize=1"));
        var second = Assert.IsType<DisputePage>(
            await customer.GetFromJsonAsync<DisputePage>("/api/disputes/page?from=2026-11-23&pageSize=1&page=2"));
        Assert.True(first.TotalCount >= 2);
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Equal(2, second.Page);
        Assert.NotEqual(Assert.Single(first.Items).Id, Assert.Single(second.Items).Id);
        var approved = await customer.GetFromJsonAsync<DisputePage>("/api/disputes/page?status=Approved");
        Assert.Equal(id, Assert.Single(approved!.Items).Id);
        var otherPage = await other.GetFromJsonAsync<DisputePage>("/api/disputes/page?from=2026-11-23&pageSize=1");
        Assert.Empty(otherPage!.Items);

        var notifications = (await customer.GetFromJsonAsync<DisputeNotificationItem[]>("/api/dispute-notifications"))!
            .Where(item => item.DisputeId == id).ToArray();
        Assert.Equal(2, notifications.Length);
        Assert.All(notifications, item => Assert.Null(item.ReadAt));
        Assert.Equal(new[] { "Approved", "Under review" }, notifications.Select(item => item.Status));

        var idToRead = notifications[0].Id;
        using var foreignRead = await other.PutAsync($"/api/dispute-notifications/{idToRead}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using var marked = await customer.PutAsync($"/api/dispute-notifications/{idToRead}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        using var repeated = await customer.PutAsync($"/api/dispute-notifications/{idToRead}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);

        using var nextSession = await SignInAsync(CustomerUser);
        var afterLogin = (await nextSession.GetFromJsonAsync<DisputeNotificationItem[]>("/api/dispute-notifications"))!
            .Where(item => item.DisputeId == id).ToArray();
        Assert.Equal(2, afterLogin.Length);
        Assert.Single(afterLogin, item => item.ReadAt is null);
        Assert.DoesNotContain((await other.GetFromJsonAsync<DisputeNotificationItem[]>("/api/dispute-notifications"))!,
            item => item.DisputeId == id);
    }

    [PostgreSqlFact]
    public async Task CustomerCanWithdrawBeforeReview()
    {
        using var customer = await SignInAsync(CustomerUser);
        using var submitted = await SubmitAsync(customer, WithdrawalTransaction);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var id = (await submitted.Content.ReadFromJsonAsync<DisputeDetails>())!.Summary.Id;

        using var withdrawn = await customer.PostAsJsonAsync($"/api/disputes/{id}/withdraw", new { });
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        using var repeat = await customer.PostAsJsonAsync($"/api/disputes/{id}/withdraw", new { });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        var receipt = await customer.GetFromJsonAsync<DisputeDetails>($"/api/disputes/{id}");
        Assert.Equal("Withdrawn", receipt!.Summary.Status);
    }

    [PostgreSqlFact]
    public async Task DuplicateSubmissionsAndStaleTransitionsCannotPersistTwice()
    {
        using var customer = await SignInAsync(CustomerUser);
        var submissions = await Task.WhenAll(SubmitAsync(customer, RaceTransaction), SubmitAsync(customer, RaceTransaction));
        using var created = Assert.Single(submissions, response => response.StatusCode == HttpStatusCode.Created);
        using var duplicate = Assert.Single(submissions, response => response.StatusCode == HttpStatusCode.Conflict);
        var id = (await created.Content.ReadFromJsonAsync<DisputeDetails>())!.Summary.Id;

        await using var customerScope = fixture.App.Services.CreateAsyncScope();
        await using var reviewerScope = fixture.App.Services.CreateAsyncScope();
        var customerStore = customerScope.ServiceProvider.GetRequiredService<IDisputeStore>();
        var reviewerStore = reviewerScope.ServiceProvider.GetRequiredService<IDisputeStore>();
        var withdrawCase = (await customerStore.FindForUpdateAsync(id, CustomerUser, default))!;
        var reviewCase = (await reviewerStore.FindForUpdateAsync(id, null, default))!;
        var withdraw = withdrawCase.Dispute.TryTransition(DisputeActor.Customer, CustomerUser,
            DisputeStatus.Withdrawn, null, Now)!;
        var review = reviewCase.Dispute.TryTransition(DisputeActor.Reviewer, ReviewerUser,
            DisputeStatus.UnderReview, null, Now)!;

        Assert.True(await customerStore.SaveTransitionAsync(withdrawCase, withdraw, default));
        Assert.False(await reviewerStore.SaveTransitionAsync(reviewCase, review, default));
        var receipt = await customer.GetFromJsonAsync<DisputeDetails>($"/api/disputes/{id}");
        Assert.Equal(new[] { "Submitted", "Withdrawn" }, receipt!.Timeline.Select(item => item.Status));
        var notifications = await customer.GetFromJsonAsync<DisputeNotificationItem[]>("/api/dispute-notifications");
        Assert.Equal("Withdrawn", Assert.Single(notifications!, item => item.DisputeId == id).Status);
        using var repeat = await SubmitAsync(customer, RaceTransaction);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [PostgreSqlFact]
    public async Task ExistingDisputeKeepsItsSubmissionDateAndCanStillBeReviewed()
    {
        using var customer = await SignInAsync(OtherUser);
        using var reviewer = await SignInAsync(ReviewerUser);
        var original = await customer.GetFromJsonAsync<DisputeDetails>($"/api/disputes/{LegacyDispute}");
        Assert.Equal("DD-LEGACY", original!.Summary.ReferenceNumber);
        var initial = Assert.Single(original!.Timeline);
        Assert.Equal(Now.AddDays(-180), initial.ChangedAt);
        Assert.Equal("Submitted", initial.Status);
        Assert.Empty((await customer.GetFromJsonAsync<DisputeNotificationItem[]>("/api/dispute-notifications"))!);

        using var review = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{LegacyDispute}/review", new { });
        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        using var rejected = await reviewer.PostAsJsonAsync($"/api/reviewer/disputes/{LegacyDispute}/decision",
            new { status = "Rejected", note = "Review evidence recorded." });
        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        var closed = await customer.GetFromJsonAsync<DisputeDetails>($"/api/disputes/{LegacyDispute}");
        Assert.Equal("Rejected", closed!.Summary.Status);
    }

    [PostgreSqlFact]
    public async Task GeneratedTransactionsPersistAcrossSessionsAndRemainCustomerScoped()
    {
        using var customer = await SignInAsync(CustomerUser);
        using var other = await SignInAsync(OtherUser);
        var before = (await customer.GetFromJsonAsync<TransactionSummary[]>("/api/transactions"))!;
        var beforeOther = (await other.GetFromJsonAsync<TransactionSummary[]>("/api/transactions"))!;
        using var generated = await customer.PostAsJsonAsync("/api/transactions/demo", new { });
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        Assert.Equal(5, await generated.Content.ReadFromJsonAsync<int>());

        using var nextSession = await SignInAsync(CustomerUser);
        var after = (await nextSession.GetFromJsonAsync<TransactionSummary[]>("/api/transactions"))!;
        var newRows = after.Where(row => before.All(existing => existing.Id != row.Id)).ToArray();
        Assert.Equal(5, newRows.Length);
        Assert.Equal(2, newRows.Count(item => item.FilingWindowExpired));
        var filtered = (await nextSession.GetFromJsonAsync<TransactionPage>(
            "/api/transactions/page?filing=Closed&search=cApE%20tOwN&from=2026-07-21&to=2026-07-21"))!;
        var match = Assert.Single(filtered.Items);
        Assert.Equal("Cape Town", match.City);
        Assert.True(match.FilingWindowExpired);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Empty((await other.GetFromJsonAsync<TransactionPage>(
            "/api/transactions/page?search=Cape%20Town"))!.Items);

        var first = (await nextSession.GetFromJsonAsync<TransactionPage>(
            "/api/transactions/page?filing=Closed&sort=Oldest&pageSize=1&page=1"))!;
        var second = (await nextSession.GetFromJsonAsync<TransactionPage>(
            "/api/transactions/page?filing=Closed&sort=Oldest&pageSize=1&page=2"))!;
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, second.Page);
        Assert.Equal("Durban", Assert.Single(first.Items).City);
        Assert.Equal(match.Id, Assert.Single(second.Items).Id);
        var amounts = (await nextSession.GetFromJsonAsync<TransactionPage>(
            "/api/transactions/page?sort=AmountHigh"))!.Items.Select(item => item.Amount).ToArray();
        Assert.Equal(amounts.OrderDescending(), amounts);
        Assert.Equal(before.Length + 5, after.Length);
        Assert.Equal(beforeOther.Select(item => item.Id),
            (await other.GetFromJsonAsync<TransactionSummary[]>("/api/transactions"))!.Select(item => item.Id));
        var dashboard = (await nextSession.GetFromJsonAsync<CustomerDashboard>("/api/dashboard"))!;
        Assert.Equal(after.Length, dashboard.TotalTransactionCount);
    }

    private async Task<HttpClient> SignInAsync(string user)
    {
        var client = fixture.App.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = user, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid transactionId) =>
        client.PostAsJsonAsync("/api/disputes", new { transactionId, reasonCode = "UNRECOGNISED" });
}
