using System.Collections.Concurrent;

namespace AuthTest.Api.Services;

public class SessionStore
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();

    public string CreateSession(Guid userId, bool enrolled)
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new SessionData(userId, enrolled);
        return token;
    }

    public SessionData? GetSession(string token) =>
        _sessions.TryGetValue(token, out var data) ? data : null;

    public void MarkEnrolled(string token)
    {
        if (_sessions.TryGetValue(token, out var data))
            _sessions[token] = data with { Enrolled = true };
    }

    public void Remove(string token) => _sessions.TryRemove(token, out _);
}

public record SessionData(Guid UserId, bool Enrolled);
