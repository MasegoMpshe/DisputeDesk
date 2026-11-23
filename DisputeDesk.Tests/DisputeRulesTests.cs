using DisputeDesk.Domain;

namespace DisputeDesk.Tests;

public sealed class DisputeRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly DisputeReason Other = new()
    {
        Code = "OTHER", Label = "Other", SortOrder = 60, RequiresDetails = true
    };

    [Fact]
    public void FilingWindowIncludesExactly120Days()
    {
        var occurredAt = Now.AddDays(-120).ToOffset(TimeSpan.FromHours(2));
        Assert.Equal(Now, Dispute.FilingDeadline(occurredAt));
        Assert.Equal(TimeSpan.Zero, Dispute.FilingDeadline(occurredAt).Offset);
        Assert.False(Dispute.IsFilingWindowExpired(occurredAt, Now.AddTicks(-1)));
        var (dispute, error) = Dispute.Raise(TransactionAt(occurredAt), Other, "  Explanation  ", Now);
        Assert.Null(error);
        Assert.Equal("Explanation", Assert.IsType<Dispute>(dispute).Details);
        Assert.Equal(DisputeFilingError.FilingWindowExpired,
            Dispute.Raise(TransactionAt(occurredAt), Other, "Explanation", Now.AddTicks(1)).Error);
    }

    [Fact]
    public void OtherRequiresNonblankDetails()
    {
        Assert.Equal(DisputeFilingError.DetailsRequired,
            Dispute.Raise(TransactionAt(Now), Other, "  ", Now).Error);
    }

    [Fact]
    public void OnlyReviewerCanReviewAndDecideWithANote()
    {
        var dispute = NewDispute();
        Assert.Null(dispute.TryTransition(DisputeActor.Customer, "customer", DisputeStatus.UnderReview, null, Now));
        Assert.Null(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.Approved, "Approved", Now));
        Assert.NotNull(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.UnderReview, null, Now));
        Assert.Null(dispute.TryTransition(DisputeActor.Customer, "customer", DisputeStatus.Withdrawn, null, Now));
        Assert.Null(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.Approved, "  ", Now));
        var decision = dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.Approved, "  Reviewed  ", Now);
        Assert.Equal("Reviewed", Assert.IsType<DisputeStatusChange>(decision).Note);
        Assert.Null(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.Rejected, "Changed my mind", Now));
        Assert.Equal(DisputeStatus.Approved, dispute.Status);
    }

    [Fact]
    public void WithdrawalClosesTheDisputeBeforeReview()
    {
        var dispute = NewDispute();
        Assert.Null(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.Withdrawn, null, Now));
        Assert.NotNull(dispute.TryTransition(DisputeActor.Customer, "customer", DisputeStatus.Withdrawn, null, Now));
        Assert.Null(dispute.TryTransition(DisputeActor.Reviewer, "reviewer", DisputeStatus.UnderReview, null, Now));
        Assert.Equal(DisputeStatus.Withdrawn, dispute.Status);
    }

    private static Dispute NewDispute() =>
        Assert.IsType<Dispute>(Dispute.Raise(TransactionAt(Now), Other, "Explanation", Now).Dispute);

    private static Transaction TransactionAt(DateTimeOffset date) => new()
    {
        Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Description = "Purchase",
        Amount = 100, Currency = "ZAR", OccurredAt = date
    };
}
