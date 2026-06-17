using BankTransactions.Server.Data;
using BankTransactions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace BankTransactions.Server.Services;

public sealed class BankingService(BankingDbContext db)
{
    private static readonly string[] CustomerTypes = ["Individual", "Legal"];
    private static readonly string[] AccountTypes = ["Salary", "Currency", "Savings"];

    public Task<List<CustomerDto>> SearchCustomersAsync(DataAccessMode mode, string? search)
    {
        return mode == DataAccessMode.Sql ? SearchCustomersSqlAsync(search) : SearchCustomersOrmAsync(search);
    }

    public async Task<CustomerDto> CreateCustomerAsync(DataAccessMode mode, CustomerCreateRequest request)
    {
        ValidateCustomer(request.CustomerType, request.FullName, request.TaxId, request.Email, request.Phone);

        if (mode == DataAccessMode.Sql)
        {
            var id = await ExecuteScalarIntAsync(
                """
                INSERT INTO Customers (CustomerType, FullName, TaxId, Email, Phone, CreatedAtUtc)
                VALUES ($type, $name, $taxId, $email, $phone, $createdAt)
                RETURNING Id
                """,
                Parameter("$type", NormalizeChoice(request.CustomerType, CustomerTypes, "CustomerType")),
                Parameter("$name", request.FullName.Trim()),
                Parameter("$taxId", request.TaxId.Trim()),
                Parameter("$email", request.Email.Trim()),
                Parameter("$phone", request.Phone.Trim()),
                Parameter("$createdAt", DateTime.UtcNow));

            return await GetCustomerAsync(id, DataAccessMode.Sql);
        }

        var customer = new Customer
        {
            CustomerType = NormalizeChoice(request.CustomerType, CustomerTypes, "CustomerType"),
            FullName = request.FullName.Trim(),
            TaxId = request.TaxId.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(int id, DataAccessMode mode, CustomerUpdateRequest request)
    {
        ValidateCustomer(request.CustomerType, request.FullName, request.TaxId, request.Email, request.Phone);

        if (mode == DataAccessMode.Sql)
        {
            var affected = await ExecuteNonQueryAsync(
                """
                UPDATE Customers
                SET CustomerType = $type, FullName = $name, TaxId = $taxId, Email = $email, Phone = $phone
                WHERE Id = $id
                """,
                Parameter("$id", id),
                Parameter("$type", NormalizeChoice(request.CustomerType, CustomerTypes, "CustomerType")),
                Parameter("$name", request.FullName.Trim()),
                Parameter("$taxId", request.TaxId.Trim()),
                Parameter("$email", request.Email.Trim()),
                Parameter("$phone", request.Phone.Trim()));

            EnsureAffected(affected, "Customer");
            return await GetCustomerAsync(id, DataAccessMode.Sql);
        }

        var customer = await db.Customers.FindAsync(id) ?? throw NotFound("Customer");
        customer.CustomerType = NormalizeChoice(request.CustomerType, CustomerTypes, "CustomerType");
        customer.FullName = request.FullName.Trim();
        customer.TaxId = request.TaxId.Trim();
        customer.Email = request.Email.Trim();
        customer.Phone = request.Phone.Trim();
        await db.SaveChangesAsync();
        return ToDto(customer);
    }

    public async Task DeleteCustomerAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var accounts = await ExecuteScalarIntAsync(
                "SELECT COUNT(*) FROM Accounts WHERE CustomerId = $id",
                Parameter("$id", id));
            if (accounts > 0)
            {
                throw new InvalidOperationException("Customer has accounts and cannot be deleted.");
            }

            EnsureAffected(await ExecuteNonQueryAsync("DELETE FROM Customers WHERE Id = $id", Parameter("$id", id)), "Customer");
            return;
        }

        var customer = await db.Customers.Include(item => item.Accounts).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw NotFound("Customer");
        if (customer.Accounts.Count > 0)
        {
            throw new InvalidOperationException("Customer has accounts and cannot be deleted.");
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
    }

    public Task<List<PartnerBankDto>> SearchPartnerBanksAsync(DataAccessMode mode, string? search)
    {
        return mode == DataAccessMode.Sql ? SearchPartnerBanksSqlAsync(search) : SearchPartnerBanksOrmAsync(search);
    }

    public async Task<PartnerBankDto> CreatePartnerBankAsync(DataAccessMode mode, PartnerBankCreateRequest request)
    {
        ValidatePartnerBank(request.Name, request.Country, request.SwiftCode, request.FeePercent);

        if (mode == DataAccessMode.Sql)
        {
            var id = await ExecuteScalarIntAsync(
                """
                INSERT INTO PartnerBanks (Name, Country, SwiftCode, IsForeign, FeePercent, CreatedAtUtc)
                VALUES ($name, $country, $swift, $isForeign, $fee, $createdAt)
                RETURNING Id
                """,
                Parameter("$name", request.Name.Trim()),
                Parameter("$country", request.Country.Trim()),
                Parameter("$swift", request.SwiftCode.Trim().ToUpperInvariant()),
                Parameter("$isForeign", request.IsForeign),
                Parameter("$fee", request.FeePercent),
                Parameter("$createdAt", DateTime.UtcNow));

            return await GetPartnerBankAsync(id, DataAccessMode.Sql);
        }

        var bank = new PartnerBank
        {
            Name = request.Name.Trim(),
            Country = request.Country.Trim(),
            SwiftCode = request.SwiftCode.Trim().ToUpperInvariant(),
            IsForeign = request.IsForeign,
            FeePercent = request.FeePercent,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PartnerBanks.Add(bank);
        await db.SaveChangesAsync();
        return ToDto(bank);
    }

    public async Task<PartnerBankDto> UpdatePartnerBankAsync(int id, DataAccessMode mode, PartnerBankUpdateRequest request)
    {
        ValidatePartnerBank(request.Name, request.Country, request.SwiftCode, request.FeePercent);

        if (mode == DataAccessMode.Sql)
        {
            var affected = await ExecuteNonQueryAsync(
                """
                UPDATE PartnerBanks
                SET Name = $name, Country = $country, SwiftCode = $swift, IsForeign = $isForeign, FeePercent = $fee
                WHERE Id = $id
                """,
                Parameter("$id", id),
                Parameter("$name", request.Name.Trim()),
                Parameter("$country", request.Country.Trim()),
                Parameter("$swift", request.SwiftCode.Trim().ToUpperInvariant()),
                Parameter("$isForeign", request.IsForeign),
                Parameter("$fee", request.FeePercent));

            EnsureAffected(affected, "Partner bank");
            return await GetPartnerBankAsync(id, DataAccessMode.Sql);
        }

        var bank = await db.PartnerBanks.FindAsync(id) ?? throw NotFound("Partner bank");
        bank.Name = request.Name.Trim();
        bank.Country = request.Country.Trim();
        bank.SwiftCode = request.SwiftCode.Trim().ToUpperInvariant();
        bank.IsForeign = request.IsForeign;
        bank.FeePercent = request.FeePercent;
        await db.SaveChangesAsync();
        return ToDto(bank);
    }

    public async Task DeletePartnerBankAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var usages = await ExecuteScalarIntAsync(
                """
                SELECT
                    (SELECT COUNT(*) FROM Accounts WHERE PartnerBankId = $id)
                    + (SELECT COUNT(*) FROM Transactions WHERE PartnerBankId = $id)
                """,
                Parameter("$id", id));
            if (usages > 0)
            {
                throw new InvalidOperationException("Partner bank is used by accounts or transactions.");
            }

            EnsureAffected(await ExecuteNonQueryAsync("DELETE FROM PartnerBanks WHERE Id = $id", Parameter("$id", id)), "Partner bank");
            return;
        }

        var used = await db.Accounts.AnyAsync(item => item.PartnerBankId == id)
            || await db.Transactions.AnyAsync(item => item.PartnerBankId == id);
        if (used)
        {
            throw new InvalidOperationException("Partner bank is used by accounts or transactions.");
        }

        var bank = await db.PartnerBanks.FindAsync(id) ?? throw NotFound("Partner bank");
        db.PartnerBanks.Remove(bank);
        await db.SaveChangesAsync();
    }

    public Task<List<AccountDto>> SearchAccountsAsync(DataAccessMode mode, string? search)
    {
        return mode == DataAccessMode.Sql ? SearchAccountsSqlAsync(search) : SearchAccountsOrmAsync(search);
    }

    public async Task<AccountDto> CreateAccountAsync(DataAccessMode mode, AccountCreateRequest request)
    {
        ValidateAccount(request.AccountType, request.Currency, request.OpeningBalance);
        await EnsureCustomerExistsAsync(request.CustomerId);
        if (request.PartnerBankId is not null)
        {
            await EnsurePartnerBankExistsAsync(request.PartnerBankId.Value);
        }

        var accountNumber = AccountNumberGenerator.Create();

        if (mode == DataAccessMode.Sql)
        {
            var id = await ExecuteScalarIntAsync(
                """
                INSERT INTO Accounts (CustomerId, AccountNumber, AccountType, Currency, Balance, PartnerBankId, IsActive, OpenedAtUtc)
                VALUES ($customerId, $number, $type, $currency, $balance, $partnerBankId, 1, $openedAt)
                RETURNING Id
                """,
                Parameter("$customerId", request.CustomerId),
                Parameter("$number", accountNumber),
                Parameter("$type", NormalizeChoice(request.AccountType, AccountTypes, "AccountType")),
                Parameter("$currency", NormalizeCurrency(request.Currency)),
                Parameter("$balance", request.OpeningBalance),
                Parameter("$partnerBankId", request.PartnerBankId),
                Parameter("$openedAt", DateTime.UtcNow));

            return await GetAccountAsync(id, DataAccessMode.Sql);
        }

        var account = new Account
        {
            CustomerId = request.CustomerId,
            AccountNumber = accountNumber,
            AccountType = NormalizeChoice(request.AccountType, AccountTypes, "AccountType"),
            Currency = NormalizeCurrency(request.Currency),
            Balance = request.OpeningBalance,
            PartnerBankId = request.PartnerBankId,
            IsActive = true,
            OpenedAtUtc = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return await GetAccountAsync(account.Id, DataAccessMode.Orm);
    }

    public async Task<AccountDto> UpdateAccountAsync(int id, DataAccessMode mode, AccountUpdateRequest request)
    {
        ValidateAccount(request.AccountType, request.Currency, 0m);
        if (request.PartnerBankId is not null)
        {
            await EnsurePartnerBankExistsAsync(request.PartnerBankId.Value);
        }

        if (mode == DataAccessMode.Sql)
        {
            var affected = await ExecuteNonQueryAsync(
                """
                UPDATE Accounts
                SET AccountType = $type, Currency = $currency, IsActive = $isActive, PartnerBankId = $partnerBankId
                WHERE Id = $id
                """,
                Parameter("$id", id),
                Parameter("$type", NormalizeChoice(request.AccountType, AccountTypes, "AccountType")),
                Parameter("$currency", NormalizeCurrency(request.Currency)),
                Parameter("$isActive", request.IsActive),
                Parameter("$partnerBankId", request.PartnerBankId));

            EnsureAffected(affected, "Account");
            return await GetAccountAsync(id, DataAccessMode.Sql);
        }

        var account = await db.Accounts.FindAsync(id) ?? throw NotFound("Account");
        account.AccountType = NormalizeChoice(request.AccountType, AccountTypes, "AccountType");
        account.Currency = NormalizeCurrency(request.Currency);
        account.IsActive = request.IsActive;
        account.PartnerBankId = request.PartnerBankId;
        await db.SaveChangesAsync();
        return await GetAccountAsync(id, DataAccessMode.Orm);
    }

    public async Task DeleteAccountAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var transactionCount = await ExecuteScalarIntAsync(
                "SELECT COUNT(*) FROM Transactions WHERE FromAccountId = $id OR ToAccountId = $id",
                Parameter("$id", id));
            if (transactionCount > 0)
            {
                throw new InvalidOperationException("Account has transactions and cannot be deleted.");
            }

            EnsureAffected(await ExecuteNonQueryAsync("DELETE FROM Accounts WHERE Id = $id", Parameter("$id", id)), "Account");
            return;
        }

        var used = await db.Transactions.AnyAsync(item => item.FromAccountId == id || item.ToAccountId == id);
        if (used)
        {
            throw new InvalidOperationException("Account has transactions and cannot be deleted.");
        }

        var account = await db.Accounts.FindAsync(id) ?? throw NotFound("Account");
        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
    }

    public Task<List<TransactionDto>> SearchTransactionsAsync(DataAccessMode mode, string? search)
    {
        return mode == DataAccessMode.Sql ? SearchTransactionsSqlAsync(search) : SearchTransactionsOrmAsync(search);
    }

    public async Task<TransactionDto> CreateTransactionAsync(DataAccessMode mode, TransactionCreateRequest request)
    {
        ValidateTransaction(request);

        return mode == DataAccessMode.Sql
            ? await CreateTransactionSqlAsync(request)
            : await CreateTransactionOrmAsync(request);
    }

    public async Task<TransactionDto> UpdateTransactionAsync(int id, DataAccessMode mode, TransactionUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        if (mode == DataAccessMode.Sql)
        {
            var affected = await ExecuteNonQueryAsync(
                "UPDATE Transactions SET Description = $description WHERE Id = $id",
                Parameter("$id", id),
                Parameter("$description", request.Description.Trim()));

            EnsureAffected(affected, "Transaction");
            return await GetTransactionAsync(id, DataAccessMode.Sql);
        }

        var transaction = await db.Transactions.FindAsync(id) ?? throw NotFound("Transaction");
        transaction.Description = request.Description.Trim();
        await db.SaveChangesAsync();
        return await GetTransactionAsync(id, DataAccessMode.Orm);
    }

    public async Task DeleteTransactionAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            await DeleteTransactionSqlAsync(id);
            return;
        }

        await using var transactionScope = await db.Database.BeginTransactionAsync();
        var transaction = await db.Transactions.FindAsync(id) ?? throw NotFound("Transaction");
        if (transaction.Status == "Completed")
        {
            if (transaction.FromAccountId is not null)
            {
                var from = await db.Accounts.FindAsync(transaction.FromAccountId.Value) ?? throw NotFound("From account");
                from.Balance += transaction.Amount + transaction.FeeAmount;
            }

            if (transaction.ToAccountId is not null)
            {
                var to = await db.Accounts.FindAsync(transaction.ToAccountId.Value) ?? throw NotFound("To account");
                to.Balance -= transaction.Amount;
            }
        }

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync();
        await transactionScope.CommitAsync();
    }

    private async Task<List<CustomerDto>> SearchCustomersOrmAsync(string? search)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(customer =>
                customer.FullName.Contains(value)
                || customer.TaxId.Contains(value)
                || customer.Email.Contains(value)
                || customer.Phone.Contains(value));
        }

        return await query.OrderBy(customer => customer.FullName).Select(customer => ToDto(customer)).ToListAsync();
    }

    private async Task<List<CustomerDto>> SearchCustomersSqlAsync(string? search)
    {
        var pattern = ToLikePattern(search);
        var rows = await QueryAsync(
            """
            SELECT Id, CustomerType, FullName, TaxId, Email, Phone, CreatedAtUtc
            FROM Customers
            WHERE $pattern = '%%'
               OR FullName LIKE $pattern
               OR TaxId LIKE $pattern
               OR Email LIKE $pattern
               OR Phone LIKE $pattern
            ORDER BY FullName
            """,
            reader => new CustomerDto(
                GetInt(reader, "Id"),
                GetString(reader, "CustomerType"),
                GetString(reader, "FullName"),
                GetString(reader, "TaxId"),
                GetString(reader, "Email"),
                GetString(reader, "Phone"),
                GetDateTime(reader, "CreatedAtUtc")),
            Parameter("$pattern", pattern));
        return rows;
    }

    private async Task<CustomerDto> GetCustomerAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var customers = await QueryAsync(
                """
                SELECT Id, CustomerType, FullName, TaxId, Email, Phone, CreatedAtUtc
                FROM Customers
                WHERE Id = $id
                """,
                reader => new CustomerDto(
                    GetInt(reader, "Id"),
                    GetString(reader, "CustomerType"),
                    GetString(reader, "FullName"),
                    GetString(reader, "TaxId"),
                    GetString(reader, "Email"),
                    GetString(reader, "Phone"),
                    GetDateTime(reader, "CreatedAtUtc")),
                Parameter("$id", id));
            return customers.FirstOrDefault() ?? throw NotFound("Customer");
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id)
            ?? throw NotFound("Customer");
        return ToDto(customer);
    }

    private async Task<List<PartnerBankDto>> SearchPartnerBanksOrmAsync(string? search)
    {
        var query = db.PartnerBanks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(bank =>
                bank.Name.Contains(value)
                || bank.Country.Contains(value)
                || bank.SwiftCode.Contains(value));
        }

        return await query.OrderBy(bank => bank.Name).Select(bank => ToDto(bank)).ToListAsync();
    }

    private async Task<List<PartnerBankDto>> SearchPartnerBanksSqlAsync(string? search)
    {
        var pattern = ToLikePattern(search);
        return await QueryAsync(
            """
            SELECT Id, Name, Country, SwiftCode, IsForeign, FeePercent, CreatedAtUtc
            FROM PartnerBanks
            WHERE $pattern = '%%'
               OR Name LIKE $pattern
               OR Country LIKE $pattern
               OR SwiftCode LIKE $pattern
            ORDER BY Name
            """,
            reader => new PartnerBankDto(
                GetInt(reader, "Id"),
                GetString(reader, "Name"),
                GetString(reader, "Country"),
                GetString(reader, "SwiftCode"),
                GetBool(reader, "IsForeign"),
                GetDecimal(reader, "FeePercent"),
                GetDateTime(reader, "CreatedAtUtc")),
            Parameter("$pattern", pattern));
    }

    private async Task<PartnerBankDto> GetPartnerBankAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var banks = await QueryAsync(
                """
                SELECT Id, Name, Country, SwiftCode, IsForeign, FeePercent, CreatedAtUtc
                FROM PartnerBanks
                WHERE Id = $id
                """,
                reader => new PartnerBankDto(
                    GetInt(reader, "Id"),
                    GetString(reader, "Name"),
                    GetString(reader, "Country"),
                    GetString(reader, "SwiftCode"),
                    GetBool(reader, "IsForeign"),
                    GetDecimal(reader, "FeePercent"),
                    GetDateTime(reader, "CreatedAtUtc")),
                Parameter("$id", id));
            return banks.FirstOrDefault() ?? throw NotFound("Partner bank");
        }

        var bank = await db.PartnerBanks.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id)
            ?? throw NotFound("Partner bank");
        return ToDto(bank);
    }

    private async Task<List<AccountDto>> SearchAccountsOrmAsync(string? search)
    {
        var query = db.Accounts
            .AsNoTracking()
            .Include(account => account.Customer)
            .Include(account => account.PartnerBank)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(account =>
                account.AccountNumber.Contains(value)
                || account.AccountType.Contains(value)
                || account.Currency.Contains(value)
                || (account.Customer != null && account.Customer.FullName.Contains(value))
                || (account.PartnerBank != null && account.PartnerBank.Name.Contains(value)));
        }

        return await query.OrderBy(account => account.AccountNumber).Select(account => ToDto(account)).ToListAsync();
    }

    private async Task<List<AccountDto>> SearchAccountsSqlAsync(string? search)
    {
        var pattern = ToLikePattern(search);
        return await QueryAsync(
            """
            SELECT a.Id, a.CustomerId, c.FullName AS CustomerName, a.AccountNumber, a.AccountType, a.Currency,
                   a.Balance, a.PartnerBankId, b.Name AS PartnerBankName, a.IsActive, a.OpenedAtUtc
            FROM Accounts a
            JOIN Customers c ON c.Id = a.CustomerId
            LEFT JOIN PartnerBanks b ON b.Id = a.PartnerBankId
            WHERE $pattern = '%%'
               OR a.AccountNumber LIKE $pattern
               OR a.AccountType LIKE $pattern
               OR a.Currency LIKE $pattern
               OR c.FullName LIKE $pattern
               OR b.Name LIKE $pattern
            ORDER BY a.AccountNumber
            """,
            ReadAccountDto,
            Parameter("$pattern", pattern));
    }

    private async Task<AccountDto> GetAccountAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var accounts = await QueryAsync(
                """
                SELECT a.Id, a.CustomerId, c.FullName AS CustomerName, a.AccountNumber, a.AccountType, a.Currency,
                       a.Balance, a.PartnerBankId, b.Name AS PartnerBankName, a.IsActive, a.OpenedAtUtc
                FROM Accounts a
                JOIN Customers c ON c.Id = a.CustomerId
                LEFT JOIN PartnerBanks b ON b.Id = a.PartnerBankId
                WHERE a.Id = $id
                """,
                ReadAccountDto,
                Parameter("$id", id));
            return accounts.FirstOrDefault() ?? throw NotFound("Account");
        }

        var account = await db.Accounts
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.PartnerBank)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw NotFound("Account");
        return ToDto(account);
    }

    private async Task<List<TransactionDto>> SearchTransactionsOrmAsync(string? search)
    {
        var query = db.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.FromAccount)
            .Include(transaction => transaction.ToAccount)
            .Include(transaction => transaction.PartnerBank)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(transaction =>
                transaction.Description.Contains(value)
                || transaction.Currency.Contains(value)
                || transaction.Status.Contains(value)
                || (transaction.FromAccount != null && transaction.FromAccount.AccountNumber.Contains(value))
                || (transaction.ToAccount != null && transaction.ToAccount.AccountNumber.Contains(value))
                || (transaction.PartnerBank != null && transaction.PartnerBank.Name.Contains(value)));
        }

        return await query.OrderByDescending(transaction => transaction.CreatedAtUtc)
            .Select(transaction => ToDto(transaction))
            .ToListAsync();
    }

    private async Task<List<TransactionDto>> SearchTransactionsSqlAsync(string? search)
    {
        var pattern = ToLikePattern(search);
        return await QueryAsync(
            """
            SELECT t.Id, t.FromAccountId, fa.AccountNumber AS FromAccountNumber, t.ToAccountId, ta.AccountNumber AS ToAccountNumber,
                   t.PartnerBankId, b.Name AS PartnerBankName, t.Amount, t.FeeAmount, t.Currency, t.Description,
                   t.Status, t.CreatedAtUtc
            FROM Transactions t
            LEFT JOIN Accounts fa ON fa.Id = t.FromAccountId
            LEFT JOIN Accounts ta ON ta.Id = t.ToAccountId
            LEFT JOIN PartnerBanks b ON b.Id = t.PartnerBankId
            WHERE $pattern = '%%'
               OR t.Description LIKE $pattern
               OR t.Currency LIKE $pattern
               OR t.Status LIKE $pattern
               OR fa.AccountNumber LIKE $pattern
               OR ta.AccountNumber LIKE $pattern
               OR b.Name LIKE $pattern
            ORDER BY t.CreatedAtUtc DESC
            """,
            ReadTransactionDto,
            Parameter("$pattern", pattern));
    }

    private async Task<TransactionDto> GetTransactionAsync(int id, DataAccessMode mode)
    {
        if (mode == DataAccessMode.Sql)
        {
            var transactions = await QueryAsync(
                """
                SELECT t.Id, t.FromAccountId, fa.AccountNumber AS FromAccountNumber, t.ToAccountId, ta.AccountNumber AS ToAccountNumber,
                       t.PartnerBankId, b.Name AS PartnerBankName, t.Amount, t.FeeAmount, t.Currency, t.Description,
                       t.Status, t.CreatedAtUtc
                FROM Transactions t
                LEFT JOIN Accounts fa ON fa.Id = t.FromAccountId
                LEFT JOIN Accounts ta ON ta.Id = t.ToAccountId
                LEFT JOIN PartnerBanks b ON b.Id = t.PartnerBankId
                WHERE t.Id = $id
                """,
                ReadTransactionDto,
                Parameter("$id", id));
            return transactions.FirstOrDefault() ?? throw NotFound("Transaction");
        }

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(item => item.FromAccount)
            .Include(item => item.ToAccount)
            .Include(item => item.PartnerBank)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw NotFound("Transaction");
        return ToDto(transaction);
    }

    private async Task<TransactionDto> CreateTransactionOrmAsync(TransactionCreateRequest request)
    {
        int id;
        await using (var transactionScope = await db.Database.BeginTransactionAsync())
        {
            var from = request.FromAccountId is null ? null : await db.Accounts.FindAsync(request.FromAccountId.Value);
            var to = request.ToAccountId is null ? null : await db.Accounts.FindAsync(request.ToAccountId.Value);
            var partnerBank = request.PartnerBankId is null ? null : await db.PartnerBanks.FindAsync(request.PartnerBankId.Value);
            ValidateTransactionAccounts(from, to, partnerBank, request);

            var fee = CalculateFee(request.Amount, partnerBank);
            if (from is not null)
            {
                var totalDebit = request.Amount + fee;
                if (from.Balance < totalDebit)
                {
                    throw new InvalidOperationException("Insufficient funds.");
                }

                from.Balance -= totalDebit;
            }

            if (to is not null)
            {
                to.Balance += request.Amount;
            }

            var item = new BankTransaction
            {
                FromAccountId = request.FromAccountId,
                ToAccountId = request.ToAccountId,
                PartnerBankId = request.PartnerBankId,
                Amount = request.Amount,
                FeeAmount = fee,
                Currency = NormalizeCurrency(request.Currency),
                Description = request.Description.Trim(),
                Status = "Completed",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Transactions.Add(item);
            await db.SaveChangesAsync();
            id = item.Id;
            await transactionScope.CommitAsync();
        }

        return await GetTransactionAsync(id, DataAccessMode.Orm);
    }

    private async Task<TransactionDto> CreateTransactionSqlAsync(TransactionCreateRequest request)
    {
        int id;
        await using (var transactionScope = await db.Database.BeginTransactionAsync())
        {
            var dbTransaction = transactionScope.GetDbTransaction();

            var from = request.FromAccountId is null ? null : await GetAccountRowAsync(request.FromAccountId.Value, dbTransaction);
            var to = request.ToAccountId is null ? null : await GetAccountRowAsync(request.ToAccountId.Value, dbTransaction);
            var partnerBank = request.PartnerBankId is null ? null : await GetPartnerBankRowAsync(request.PartnerBankId.Value, dbTransaction);
            ValidateTransactionAccounts(from, to, partnerBank, request);

            var fee = CalculateFee(request.Amount, partnerBank);
            if (from is not null)
            {
                var totalDebit = request.Amount + fee;
                if (from.Balance < totalDebit)
                {
                    throw new InvalidOperationException("Insufficient funds.");
                }

                await ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance - $amount WHERE Id = $id",
                    dbTransaction,
                    Parameter("$amount", totalDebit),
                    Parameter("$id", from.Id));
            }

            if (to is not null)
            {
                await ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance + $amount WHERE Id = $id",
                    dbTransaction,
                    Parameter("$amount", request.Amount),
                    Parameter("$id", to.Id));
            }

            id = await ExecuteScalarIntAsync(
                """
                INSERT INTO Transactions (FromAccountId, ToAccountId, PartnerBankId, Amount, FeeAmount, Currency, Description, Status, CreatedAtUtc)
                VALUES ($from, $to, $partner, $amount, $fee, $currency, $description, 'Completed', $createdAt)
                RETURNING Id
                """,
                dbTransaction,
                Parameter("$from", request.FromAccountId),
                Parameter("$to", request.ToAccountId),
                Parameter("$partner", request.PartnerBankId),
                Parameter("$amount", request.Amount),
                Parameter("$fee", fee),
                Parameter("$currency", NormalizeCurrency(request.Currency)),
                Parameter("$description", request.Description.Trim()),
                Parameter("$createdAt", DateTime.UtcNow));

            await transactionScope.CommitAsync();
        }

        return await GetTransactionAsync(id, DataAccessMode.Sql);
    }

    private async Task DeleteTransactionSqlAsync(int id)
    {
        await using var transactionScope = await db.Database.BeginTransactionAsync();
        var dbTransaction = transactionScope.GetDbTransaction();

        var transactions = await QueryAsync(
            """
            SELECT t.Id, t.FromAccountId, fa.AccountNumber AS FromAccountNumber, t.ToAccountId, ta.AccountNumber AS ToAccountNumber,
                   t.PartnerBankId, b.Name AS PartnerBankName, t.Amount, t.FeeAmount, t.Currency, t.Description,
                   t.Status, t.CreatedAtUtc
            FROM Transactions t
            LEFT JOIN Accounts fa ON fa.Id = t.FromAccountId
            LEFT JOIN Accounts ta ON ta.Id = t.ToAccountId
            LEFT JOIN PartnerBanks b ON b.Id = t.PartnerBankId
            WHERE t.Id = $id
            """,
            dbTransaction,
            ReadTransactionDto,
            Parameter("$id", id));

        var item = transactions.FirstOrDefault() ?? throw NotFound("Transaction");
        if (item.Status == "Completed")
        {
            if (item.FromAccountId is not null)
            {
                await ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance + $amount WHERE Id = $id",
                    dbTransaction,
                    Parameter("$amount", item.Amount + item.FeeAmount),
                    Parameter("$id", item.FromAccountId.Value));
            }

            if (item.ToAccountId is not null)
            {
                await ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance - $amount WHERE Id = $id",
                    dbTransaction,
                    Parameter("$amount", item.Amount),
                    Parameter("$id", item.ToAccountId.Value));
            }
        }

        EnsureAffected(
            await ExecuteNonQueryAsync("DELETE FROM Transactions WHERE Id = $id", dbTransaction, Parameter("$id", id)),
            "Transaction");
        await transactionScope.CommitAsync();
    }

    private async Task EnsureCustomerExistsAsync(int customerId)
    {
        if (!await db.Customers.AnyAsync(item => item.Id == customerId))
        {
            throw NotFound("Customer");
        }
    }

    private async Task EnsurePartnerBankExistsAsync(int partnerBankId)
    {
        if (!await db.PartnerBanks.AnyAsync(item => item.Id == partnerBankId))
        {
            throw NotFound("Partner bank");
        }
    }

    private static void ValidateCustomer(string customerType, string fullName, string taxId, string email, string phone)
    {
        _ = NormalizeChoice(customerType, CustomerTypes, "CustomerType");
        Require(fullName, "FullName");
        Require(taxId, "TaxId");
        Require(email, "Email");
        Require(phone, "Phone");
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email must contain @.");
        }
    }

    private static void ValidatePartnerBank(string name, string country, string swiftCode, decimal feePercent)
    {
        Require(name, "Name");
        Require(country, "Country");
        Require(swiftCode, "SwiftCode");
        if (swiftCode.Trim().Length is < 8 or > 11)
        {
            throw new ArgumentException("SwiftCode must contain 8-11 characters.");
        }

        if (feePercent < 0m || feePercent > 20m)
        {
            throw new ArgumentException("FeePercent must be between 0 and 20.");
        }
    }

    private static void ValidateAccount(string accountType, string currency, decimal openingBalance)
    {
        _ = NormalizeChoice(accountType, AccountTypes, "AccountType");
        _ = NormalizeCurrency(currency);
        if (openingBalance < 0m)
        {
            throw new ArgumentException("OpeningBalance must be non-negative.");
        }
    }

    private static void ValidateTransaction(TransactionCreateRequest request)
    {
        if (request.FromAccountId is null && request.ToAccountId is null)
        {
            throw new ArgumentException("At least one account is required.");
        }

        if (request.FromAccountId == request.ToAccountId)
        {
            throw new ArgumentException("Source and destination accounts must be different.");
        }

        if (request.Amount <= 0m)
        {
            throw new ArgumentException("Amount must be positive.");
        }

        _ = NormalizeCurrency(request.Currency);
        Require(request.Description, "Description");
    }

    private static void ValidateTransactionAccounts(
        Account? from,
        Account? to,
        PartnerBank? partnerBank,
        TransactionCreateRequest request)
    {
        if (request.FromAccountId is not null && from is null)
        {
            throw NotFound("From account");
        }

        if (request.ToAccountId is not null && to is null)
        {
            throw NotFound("To account");
        }

        if (request.PartnerBankId is not null && partnerBank is null)
        {
            throw NotFound("Partner bank");
        }

        if (from is not null && !from.IsActive)
        {
            throw new InvalidOperationException("Source account is not active.");
        }

        if (to is not null && !to.IsActive)
        {
            throw new InvalidOperationException("Destination account is not active.");
        }

        if (from is not null && !string.Equals(from.Currency, NormalizeCurrency(request.Currency), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Source account currency does not match transaction currency.");
        }

        if (to is not null && !string.Equals(to.Currency, NormalizeCurrency(request.Currency), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Destination account currency does not match transaction currency.");
        }
    }

    private static void ValidateTransactionAccounts(
        AccountRow? from,
        AccountRow? to,
        PartnerBankRow? partnerBank,
        TransactionCreateRequest request)
    {
        if (request.FromAccountId is not null && from is null)
        {
            throw NotFound("From account");
        }

        if (request.ToAccountId is not null && to is null)
        {
            throw NotFound("To account");
        }

        if (request.PartnerBankId is not null && partnerBank is null)
        {
            throw NotFound("Partner bank");
        }

        if (from is not null && !from.IsActive)
        {
            throw new InvalidOperationException("Source account is not active.");
        }

        if (to is not null && !to.IsActive)
        {
            throw new InvalidOperationException("Destination account is not active.");
        }

        var currency = NormalizeCurrency(request.Currency);
        if (from is not null && !string.Equals(from.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Source account currency does not match transaction currency.");
        }

        if (to is not null && !string.Equals(to.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Destination account currency does not match transaction currency.");
        }
    }

    private static decimal CalculateFee(decimal amount, PartnerBank? partnerBank)
    {
        return partnerBank is null ? 0m : Math.Round(amount * partnerBank.FeePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateFee(decimal amount, PartnerBankRow? partnerBank)
    {
        return partnerBank is null ? 0m : Math.Round(amount * partnerBank.FeePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<AccountRow?> GetAccountRowAsync(int id, DbTransaction transaction)
    {
        var rows = await QueryAsync(
            """
            SELECT Id, Currency, Balance, IsActive
            FROM Accounts
            WHERE Id = $id
            """,
            transaction,
            reader => new AccountRow(
                GetInt(reader, "Id"),
                GetString(reader, "Currency"),
                GetDecimal(reader, "Balance"),
                GetBool(reader, "IsActive")),
            Parameter("$id", id));
        return rows.FirstOrDefault();
    }

    private async Task<PartnerBankRow?> GetPartnerBankRowAsync(int id, DbTransaction transaction)
    {
        var rows = await QueryAsync(
            """
            SELECT Id, FeePercent
            FROM PartnerBanks
            WHERE Id = $id
            """,
            transaction,
            reader => new PartnerBankRow(GetInt(reader, "Id"), GetDecimal(reader, "FeePercent")),
            Parameter("$id", id));
        return rows.FirstOrDefault();
    }

    private static AccountDto ReadAccountDto(DbDataReader reader)
    {
        return new AccountDto(
            GetInt(reader, "Id"),
            GetInt(reader, "CustomerId"),
            GetString(reader, "CustomerName"),
            GetString(reader, "AccountNumber"),
            GetString(reader, "AccountType"),
            GetString(reader, "Currency"),
            GetDecimal(reader, "Balance"),
            GetNullableInt(reader, "PartnerBankId"),
            GetNullableString(reader, "PartnerBankName"),
            GetBool(reader, "IsActive"),
            GetDateTime(reader, "OpenedAtUtc"));
    }

    private static TransactionDto ReadTransactionDto(DbDataReader reader)
    {
        return new TransactionDto(
            GetInt(reader, "Id"),
            GetNullableInt(reader, "FromAccountId"),
            GetNullableString(reader, "FromAccountNumber"),
            GetNullableInt(reader, "ToAccountId"),
            GetNullableString(reader, "ToAccountNumber"),
            GetNullableInt(reader, "PartnerBankId"),
            GetNullableString(reader, "PartnerBankName"),
            GetDecimal(reader, "Amount"),
            GetDecimal(reader, "FeeAmount"),
            GetString(reader, "Currency"),
            GetString(reader, "Description"),
            GetString(reader, "Status"),
            GetDateTime(reader, "CreatedAtUtc"));
    }

    private static CustomerDto ToDto(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.CustomerType,
            customer.FullName,
            customer.TaxId,
            customer.Email,
            customer.Phone,
            customer.CreatedAtUtc);
    }

    private static PartnerBankDto ToDto(PartnerBank bank)
    {
        return new PartnerBankDto(
            bank.Id,
            bank.Name,
            bank.Country,
            bank.SwiftCode,
            bank.IsForeign,
            bank.FeePercent,
            bank.CreatedAtUtc);
    }

    private static AccountDto ToDto(Account account)
    {
        return new AccountDto(
            account.Id,
            account.CustomerId,
            account.Customer?.FullName ?? string.Empty,
            account.AccountNumber,
            account.AccountType,
            account.Currency,
            account.Balance,
            account.PartnerBankId,
            account.PartnerBank?.Name,
            account.IsActive,
            account.OpenedAtUtc);
    }

    private static TransactionDto ToDto(BankTransaction transaction)
    {
        return new TransactionDto(
            transaction.Id,
            transaction.FromAccountId,
            transaction.FromAccount?.AccountNumber,
            transaction.ToAccountId,
            transaction.ToAccount?.AccountNumber,
            transaction.PartnerBankId,
            transaction.PartnerBank?.Name,
            transaction.Amount,
            transaction.FeeAmount,
            transaction.Currency,
            transaction.Description,
            transaction.Status,
            transaction.CreatedAtUtc);
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        params SqlParameterValue[] parameters)
    {
        return await QueryAsync(sql, transaction: null, map, parameters);
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        DbTransaction? transaction,
        Func<DbDataReader, T> map,
        params SqlParameterValue[] parameters)
    {
        var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        AddParameters(command, parameters);

        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private async Task<int> ExecuteScalarIntAsync(string sql, params SqlParameterValue[] parameters)
    {
        return await ExecuteScalarIntAsync(sql, transaction: null, parameters);
    }

    private async Task<int> ExecuteScalarIntAsync(string sql, DbTransaction? transaction, params SqlParameterValue[] parameters)
    {
        var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        AddParameters(command, parameters);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameterValue[] parameters)
    {
        return await ExecuteNonQueryAsync(sql, transaction: null, parameters);
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, DbTransaction? transaction, params SqlParameterValue[] parameters)
    {
        var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    private static void AddParameters(DbCommand command, IEnumerable<SqlParameterValue> parameters)
    {
        foreach (var parameterValue in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterValue.Name;
            parameter.Value = parameterValue.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static SqlParameterValue Parameter(string name, object? value)
    {
        return new SqlParameterValue(name, value);
    }

    private static string NormalizeChoice(string value, IReadOnlyCollection<string> allowed, string field)
    {
        Require(value, field);
        var normalized = allowed.FirstOrDefault(item => string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new ArgumentException($"{field} has unsupported value.");
    }

    private static string NormalizeCurrency(string value)
    {
        Require(value, "Currency");
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(item => item < 'A' || item > 'Z'))
        {
            throw new ArgumentException("Currency must be a three-letter ISO code.");
        }

        return normalized;
    }

    private static void Require(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.");
        }
    }

    private static string ToLikePattern(string? search)
    {
        return string.IsNullOrWhiteSpace(search) ? "%%" : $"%{search.Trim()}%";
    }

    private static void EnsureAffected(int affected, string entityName)
    {
        if (affected == 0)
        {
            throw NotFound(entityName);
        }
    }

    private static KeyNotFoundException NotFound(string entityName)
    {
        return new KeyNotFoundException($"{entityName} was not found.");
    }

    private static int GetInt(DbDataReader reader, string name)
    {
        return Convert.ToInt32(reader[name]);
    }

    private static int? GetNullableInt(DbDataReader reader, string name)
    {
        return reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);
    }

    private static string GetString(DbDataReader reader, string name)
    {
        return Convert.ToString(reader[name]) ?? string.Empty;
    }

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        return reader[name] is DBNull ? null : Convert.ToString(reader[name]);
    }

    private static decimal GetDecimal(DbDataReader reader, string name)
    {
        return reader[name] switch
        {
            decimal value => value,
            double value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            float value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            string value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture),
            var value => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }

    private static bool GetBool(DbDataReader reader, string name)
    {
        return Convert.ToBoolean(reader[name]);
    }

    private static DateTime GetDateTime(DbDataReader reader, string name)
    {
        return Convert.ToDateTime(reader[name]);
    }

    private readonly record struct SqlParameterValue(string Name, object? Value);

    private sealed record AccountRow(int Id, string Currency, decimal Balance, bool IsActive);

    private sealed record PartnerBankRow(int Id, decimal FeePercent);
}
