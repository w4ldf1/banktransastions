namespace BankTransactions.Server.Data;

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
}

public sealed class Customer
{
    public int Id { get; set; }
    public required string CustomerType { get; set; }
    public required string FullName { get; set; }
    public required string TaxId { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<Account> Accounts { get; set; } = [];
}

public sealed class PartnerBank
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Country { get; set; }
    public required string SwiftCode { get; set; }
    public bool IsForeign { get; set; }
    public decimal FeePercent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Account
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string AccountNumber { get; set; }
    public required string AccountType { get; set; }
    public required string Currency { get; set; }
    public decimal Balance { get; set; }
    public int? PartnerBankId { get; set; }
    public bool IsActive { get; set; }
    public DateTime OpenedAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public PartnerBank? PartnerBank { get; set; }
}

public sealed class BankTransaction
{
    public int Id { get; set; }
    public int? FromAccountId { get; set; }
    public int? ToAccountId { get; set; }
    public int? PartnerBankId { get; set; }
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public required string Currency { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Account? FromAccount { get; set; }
    public Account? ToAccount { get; set; }
    public PartnerBank? PartnerBank { get; set; }
}
