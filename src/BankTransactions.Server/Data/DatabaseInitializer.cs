using BankTransactions.Server.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BankTransactions.Server.Data;

public sealed class DatabaseInitializer(BankingDbContext db, PasswordHasher passwordHasher)
{
    public async Task InitializeAsync()
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = passwordHasher.Hash("admin123"),
                Role = "Administrator"
            });
        }

        if (!await db.PartnerBanks.AnyAsync())
        {
            db.PartnerBanks.AddRange(
                new PartnerBank
                {
                    Name = "National Clearing Bank",
                    Country = "Russia",
                    SwiftCode = "NCBMRUMM",
                    IsForeign = false,
                    FeePercent = 0.20m,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new PartnerBank
                {
                    Name = "Euro Payment Partner",
                    Country = "Germany",
                    SwiftCode = "EUPPDEFF",
                    IsForeign = true,
                    FeePercent = 1.15m,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }

        if (!await db.Customers.AnyAsync())
        {
            var individual = new Customer
            {
                CustomerType = "Individual",
                FullName = "Ivan Petrov",
                TaxId = "770100000001",
                Email = "ivan.petrov@example.com",
                Phone = "+79990000001",
                CreatedAtUtc = DateTime.UtcNow
            };
            var company = new Customer
            {
                CustomerType = "Legal",
                FullName = "OOO Meridian",
                TaxId = "7701000002",
                Email = "finance@meridian.example",
                Phone = "+79990000002",
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Customers.AddRange(individual, company);
            await db.SaveChangesAsync();

            db.Accounts.AddRange(
                new Account
                {
                    CustomerId = individual.Id,
                    AccountNumber = AccountNumberGenerator.Create(),
                    AccountType = "Salary",
                    Currency = "RUB",
                    Balance = 125_000m,
                    IsActive = true,
                    OpenedAtUtc = DateTime.UtcNow
                },
                new Account
                {
                    CustomerId = company.Id,
                    AccountNumber = AccountNumberGenerator.Create(),
                    AccountType = "Savings",
                    Currency = "RUB",
                    Balance = 460_000m,
                    IsActive = true,
                    OpenedAtUtc = DateTime.UtcNow
                });
        }

        await db.SaveChangesAsync();
    }
}

public static class AccountNumberGenerator
{
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var number = BitConverter.ToUInt64(bytes) % 10_000_000_000_000_000UL;
        return $"40817810{number:D16}";
    }
}
