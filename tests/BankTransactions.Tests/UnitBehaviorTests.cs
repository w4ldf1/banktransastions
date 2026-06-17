using BankTransactions.Server.Data;
using BankTransactions.Shared;

namespace BankTransactions.Tests;

public sealed class UnitBehaviorTests
{
    [Theory]
    [InlineData("sql", DataAccessMode.Sql)]
    [InlineData("SQL", DataAccessMode.Sql)]
    [InlineData("orm", DataAccessMode.Orm)]
    [InlineData("", DataAccessMode.Orm)]
    [InlineData(null, DataAccessMode.Orm)]
    public void DataAccessModeParser_Parse_ReturnsExpectedMode(string? value, DataAccessMode expected)
    {
        var actual = DataAccessModeParser.Parse(value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AccountNumberGenerator_Create_ReturnsRussianAccountLikeNumber()
    {
        var accountNumber = AccountNumberGenerator.Create();

        Assert.StartsWith("40817810", accountNumber);
        Assert.Equal(24, accountNumber.Length);
        Assert.All(accountNumber, character => Assert.True(char.IsDigit(character)));
    }
}
