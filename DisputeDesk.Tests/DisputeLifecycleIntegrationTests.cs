using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DisputeDesk.Application.Dashboard;
using DisputeDesk.Application.Disputes;
using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static DisputeDesk.Tests.PostgreSqlDisputeFixture;

namespace DisputeDesk.Tests;

public sealed class DisputeLifecycleIntegrationTests(PostgreSqlDisputeFixture fixture)
    : IClassFixture<PostgreSqlDisputeFixture>
{
    [PostgreSqlFact]
    public async Task ReviewerDecisionUpdatesTheCustomerTimelineAndNotifications()
    {
        using var customer = await SignInAsync(CustomerUser);
        using var reviewer = await SignInAsync(ReviewerUser);
        using var other = await SignInAsync(OtherUser);
        var session = await reviewer.GetFromJsonAsync<JsonElement>("/api/auth/session");
        Assert.Equal("Reviewer", session.GetProperty("role").GetString());

        using var early = await SubmitAsync(customer, OldTransaction);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains("120 days", await early.Content.ReadAsStringAsync());
        using var foreign = await SubmitAsync(customer, OtherTransaction);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        using var submitted = await SubmitAsync(customer, BoundaryTransaction);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var dispute = Assert.IsType<DisputeDetails>(await submitted.Content.ReadFromJsonAsync<DisputeDetails>());
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
