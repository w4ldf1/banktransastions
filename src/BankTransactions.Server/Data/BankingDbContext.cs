using Microsoft.EntityFrameworkCore;

namespace BankTransactions.Server.Data;

public sealed class BankingDbContext(DbContextOptions<BankingDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PartnerBank> PartnerBanks => Set<PartnerBank>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BankTransaction> Transactions => Set<BankTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.Username).HasMaxLength(64);
            entity.Property(user => user.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(customer => customer.TaxId).IsUnique();
            entity.Property(customer => customer.CustomerType).HasMaxLength(24);
            entity.Property(customer => customer.FullName).HasMaxLength(160);
            entity.Property(customer => customer.TaxId).HasMaxLength(32);
            entity.Property(customer => customer.Email).HasMaxLength(160);
            entity.Property(customer => customer.Phone).HasMaxLength(32);
        });

        modelBuilder.Entity<PartnerBank>(entity =>
        {
            entity.HasIndex(bank => bank.SwiftCode).IsUnique();
            entity.Property(bank => bank.Name).HasMaxLength(160);
            entity.Property(bank => bank.Country).HasMaxLength(80);
            entity.Property(bank => bank.SwiftCode).HasMaxLength(16);
            entity.Property(bank => bank.FeePercent).HasPrecision(9, 4);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(account => account.AccountNumber).IsUnique();
            entity.Property(account => account.AccountNumber).HasMaxLength(34);
            entity.Property(account => account.AccountType).HasMaxLength(24);
            entity.Property(account => account.Currency).HasMaxLength(3);
            entity.Property(account => account.Balance).HasPrecision(18, 2);
            entity.HasOne(account => account.Customer)
                .WithMany(customer => customer.Accounts)
                .HasForeignKey(account => account.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(account => account.PartnerBank)
                .WithMany()
                .HasForeignKey(account => account.PartnerBankId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Amount).HasPrecision(18, 2);
            entity.Property(transaction => transaction.FeeAmount).HasPrecision(18, 2);
            entity.Property(transaction => transaction.Currency).HasMaxLength(3);
            entity.Property(transaction => transaction.Description).HasMaxLength(240);
            entity.Property(transaction => transaction.Status).HasMaxLength(24);
            entity.HasOne(transaction => transaction.FromAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.ToAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.PartnerBank)
                .WithMany()
                .HasForeignKey(transaction => transaction.PartnerBankId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
