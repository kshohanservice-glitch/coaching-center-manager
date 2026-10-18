using System.Collections.Concurrent;
using System.Security.Cryptography;
using ShikkhaDesk.Application.Security;

namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// In-memory session table. Tokens are random, never derived from the password, and expire on a
/// sliding window. Sessions are intentionally not persisted: a host restart forces a clean re-login.
/// </summary>
public sealed class LanSessionStore
{
    private sealed record Entry(Principal Principal, DateTime ExpiresAtUtc, string DeviceId);

    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime;

    public LanSessionStore(TimeSpan lifetime) => _lifetime = lifetime;

    public string Issue(Principal principal)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = new Entry(principal, DateTime.UtcNow.Add(_lifetime), principal.DeviceId);
        return token;
    }

    public Principal? Resolve(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token, out Entry? entry))
        {
            return null;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        // Sliding expiry: an active operator is not logged out mid-transaction.
        if (entry.ExpiresAtUtc - DateTime.UtcNow < _lifetime - TimeSpan.FromMinutes(5))
        {
            _sessions[token] = entry with { ExpiresAtUtc = DateTime.UtcNow.Add(_lifetime) };
        }

        return entry.Principal;
    }

    public void Revoke(string? token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _sessions.TryRemove(token, out _);
        }
    }

    public int ActiveCount => _sessions.Count;

    public int Prune()
    {
        DateTime now = DateTime.UtcNow;
        int removed = 0;

        foreach (KeyValuePair<string, Entry> session in _sessions)
        {
            if (session.Value.ExpiresAtUtc <= now && _sessions.TryRemove(session.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }
}
