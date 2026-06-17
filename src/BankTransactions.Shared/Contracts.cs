namespace BankTransactions.Shared;

public enum DataAccessMode
{
    Orm,
    Sql
}

public static class DataAccessModeParser
{
    public static DataAccessMode Parse(string? value)
    {
        return string.Equals(value, "sql", StringComparison.OrdinalIgnoreCase)
            ? DataAccessMode.Sql
            : DataAccessMode.Orm;
    }
}

public sealed record ApiError(string Message);

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(string Token, string Username, string Role);

public sealed record CustomerDto(
    int Id,
    string CustomerType,
    string FullName,
    string TaxId,
    string Email,
    string Phone,
    DateTime CreatedAtUtc);

public sealed record CustomerCreateRequest(
    string CustomerType,
    string FullName,
    string TaxId,
    string Email,
    string Phone);

public sealed record CustomerUpdateRequest(
    string CustomerType,
    string FullName,
    string TaxId,
    string Email,
    string Phone);

public sealed record PartnerBankDto(
    int Id,
    string Name,
    string Country,
    string SwiftCode,
    bool IsForeign,
    decimal FeePercent,
    DateTime CreatedAtUtc);

public sealed record PartnerBankCreateRequest(
    string Name,
    string Country,
    string SwiftCode,
    bool IsForeign,
    decimal FeePercent);

public sealed record PartnerBankUpdateRequest(
    string Name,
    string Country,
    string SwiftCode,
    bool IsForeign,
    decimal FeePercent);

public sealed record AccountDto(
    int Id,
    int CustomerId,
    string CustomerName,
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal Balance,
    int? PartnerBankId,
    string? PartnerBankName,
    bool IsActive,
    DateTime OpenedAtUtc);

public sealed record AccountCreateRequest(
    int CustomerId,
    string AccountType,
    string Currency,
    decimal OpeningBalance,
    int? PartnerBankId);

public sealed record AccountUpdateRequest(
    string AccountType,
    string Currency,
    bool IsActive,
    int? PartnerBankId);

public sealed record TransactionDto(
    int Id,
    int? FromAccountId,
    string? FromAccountNumber,
    int? ToAccountId,
    string? ToAccountNumber,
    int? PartnerBankId,
    string? PartnerBankName,
    decimal Amount,
    decimal FeeAmount,
    string Currency,
    string Description,
    string Status,
    DateTime CreatedAtUtc);

public sealed record TransactionCreateRequest(
    int? FromAccountId,
    int? ToAccountId,
    int? PartnerBankId,
    decimal Amount,
    string Currency,
    string Description);

public sealed record TransactionUpdateRequest(string Description);
