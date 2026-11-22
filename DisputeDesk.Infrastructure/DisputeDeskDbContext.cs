using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeDeskDbContext(DbContextOptions<DisputeDeskDbContext> options)
    : IdentityUserContext<IdentityUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Dispute> Disputes => Set<Dispute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var customers = modelBuilder.Entity<Customer>();
        customers.ToTable("customers");
        customers.HasKey(customer => customer.Id);
        customers.Property(customer => customer.Id).HasColumnName("id");
        customers.Property(customer => customer.IdentityUserId).HasColumnName("identity_user_id");
        customers.Property(customer => customer.DisplayName).HasColumnName("display_name");
        customers.HasIndex(customer => customer.IdentityUserId).IsUnique();

        var transactions = modelBuilder.Entity<Transaction>();
        transactions.ToTable("transactions");
        transactions.HasKey(transaction => transaction.Id);
        transactions.Property(transaction => transaction.Id).HasColumnName("id");
        transactions.Property(transaction => transaction.CustomerId).HasColumnName("customer_id");
        transactions.Property(transaction => transaction.Description).HasColumnName("description");
        transactions.Property(transaction => transaction.Amount).HasColumnName("amount").HasPrecision(18, 2);
        transactions.Property(transaction => transaction.Currency).HasColumnName("currency").HasMaxLength(3);
        transactions.Property(transaction => transaction.OccurredAt).HasColumnName("occurred_at");
        transactions.HasIndex(transaction => new { transaction.CustomerId, transaction.OccurredAt });
        transactions.HasOne<Customer>().WithMany()
            .HasForeignKey(transaction => transaction.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        var disputes = modelBuilder.Entity<Dispute>();
        disputes.ToTable("disputes");
        disputes.HasKey(dispute => dispute.Id);
        disputes.Property(dispute => dispute.Id).HasColumnName("id");
        disputes.Property(dispute => dispute.TransactionId).HasColumnName("transaction_id");
        disputes.Property(dispute => dispute.ReferenceNumber).HasColumnName("reference_number");
        disputes.Property(dispute => dispute.Reason).HasColumnName("reason");
        disputes.Property(dispute => dispute.Details).HasColumnName("details");
        disputes.Property(dispute => dispute.Status).HasColumnName("status");
        disputes.Property(dispute => dispute.SubmittedAt).HasColumnName("submitted_at");
        disputes.HasIndex(dispute => dispute.ReferenceNumber).IsUnique();
        disputes.HasOne<Transaction>().WithOne()
            .HasForeignKey<Dispute>(dispute => dispute.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
