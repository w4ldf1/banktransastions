using BankTransactions.Server.Data;
using BankTransactions.Server.Security;
using BankTransactions.Server.Services;
using BankTransactions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace BankTransactions.Tests;

public sealed class ServiceFlowTests
{
    [Fact]
    public async Task CustomerCrud_WorksThroughSqlAndOrmModes()
    {
        await using var fixture = await ServiceFixture.CreateAsync();

        var created = await fixture.Service.CreateCustomerAsync(
            DataAccessMode.Sql,
            new CustomerCreateRequest("Legal", "OOO Vector Test", "7701999001", "billing@vector.example", "+79991110022"));

        var updated = await fixture.Service.UpdateCustomerAsync(
            created.Id,
            DataAccessMode.Orm,
            new CustomerUpdateRequest("Legal", "OOO Vector Test Updated", "7701999001", "updated@vector.example", "+79991110022"));

        Assert.Contains("Updated", updated.FullName, StringComparison.Ordinal);

        var found = await fixture.Service.SearchCustomersAsync(DataAccessMode.Sql, "Updated");
        Assert.Contains(found, item => item.Id == created.Id && item.FullName.Contains("Updated", StringComparison.Ordinal));

        await fixture.Service.DeleteCustomerAsync(created.Id, DataAccessMode.Orm);

        var afterDelete = await fixture.Service.SearchCustomersAsync(DataAccessMode.Sql, "Vector");
        Assert.DoesNotContain(afterDelete, item => item.Id == created.Id);
    }

    [Fact]
    public async Task EndToEndTransfer_RecalculatesBalancesAndStoresTransaction()
    {
        await using var fixture = await ServiceFixture.CreateAsync();

        var customer = await fixture.Service.CreateCustomerAsync(
            DataAccessMode.Orm,
            new CustomerCreateRequest("Individual", "Transfer Test Customer", Guid.NewGuid().ToString("N")[..12], "transfer@test.example", "+79990009900"));

        var partners = await fixture.Service.SearchPartnerBanksAsync(DataAccessMode.Orm, "NCBMRUMM");
        var partner = Assert.Single(partners.Where(item => item.SwiftCode == "NCBMRUMM"));

        var from = await fixture.Service.CreateAccountAsync(
            DataAccessMode.Sql,
            new AccountCreateRequest(customer.Id, "Savings", "RUB", 1000m, null));
        var to = await fixture.Service.CreateAccountAsync(
            DataAccessMode.Orm,
            new AccountCreateRequest(customer.Id, "Savings", "RUB", 50m, null));

        var transfer = await fixture.Service.CreateTransactionAsync(
            DataAccessMode.Sql,
            new TransactionCreateRequest(from.Id, to.Id, partner.Id, 150m, "RUB", "Test transfer"));

        Assert.Equal(150m, transfer.Amount);
        Assert.Equal(0.30m, transfer.FeeAmount);

        var accounts = await fixture.Service.SearchAccountsAsync(DataAccessMode.Orm, "40817810");
        var updatedFrom = Assert.Single(accounts.Where(item => item.Id == from.Id));
        var updatedTo = Assert.Single(accounts.Where(item => item.Id == to.Id));

        Assert.Equal(849.70m, updatedFrom.Balance);
        Assert.Equal(200m, updatedTo.Balance);

        var transactions = await fixture.Service.SearchTransactionsAsync(DataAccessMode.Orm, "Test transfer");
        Assert.Contains(transactions, item => item.Id == transfer.Id && item.Status == "Completed");
    }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private ServiceFixture(string databasePath, BankingDbContext db)
        {
            _databasePath = databasePath;
            Db = db;
            Service = new BankingService(Db);
        }

        public BankingDbContext Db { get; }

        public BankingService Service { get; }

        public static async Task<ServiceFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"bank-transactions-service-tests-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<BankingDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;
            var db = new BankingDbContext(options);
            var initializer = new DatabaseInitializer(db, new PasswordHasher());
            await initializer.InitializeAsync();
            return new ServiceFixture(databasePath, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            SqliteConnection.ClearAllPools();

            foreach (var path in new[] { _databasePath, $"{_databasePath}-shm", $"{_databasePath}-wal" })
            {
                TryDelete(path);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
