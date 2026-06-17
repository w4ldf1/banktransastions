using BankTransactions.Server.Security;

namespace BankTransactions.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_DoesNotStorePasswordAndVerifiesOriginalValue()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash("admin123");

        Assert.DoesNotContain("admin123", hash, StringComparison.Ordinal);
        Assert.True(hasher.Verify("admin123", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }
}
