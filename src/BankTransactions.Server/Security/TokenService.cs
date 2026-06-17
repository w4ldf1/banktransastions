using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BankTransactions.Server.Security;

public sealed record AuthenticatedUser(int Id, string Username, string Role);

public sealed class TokenService
{
    private readonly ConcurrentDictionary<string, AuthenticatedUser> _sessions = new();

    public string Issue(AuthenticatedUser user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[token] = user;
        return token;
    }

    public AuthenticatedUser? Validate(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string prefix = "Bearer ";
        var token = authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : authorizationHeader.Trim();

        return _sessions.TryGetValue(token, out var user) ? user : null;
    }
}
