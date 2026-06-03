using AuthTest.Api.Data;
using AuthTest.Api.Models;
using Fido2NetLib;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Api.Services;

public class ChallengeStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private readonly AppDbContext _db;

    public ChallengeStore(AppDbContext db) => _db = db;

    public async Task StoreRegisterOptionsAsync(string key, CredentialCreateOptions options)
    {
        await PurgeExpiredAsync();
        var existing = await _db.Challenges
            .Where(c => c.Key == key && c.Type == "register")
            .ToListAsync();
        _db.Challenges.RemoveRange(existing);
        _db.Challenges.Add(new WebAuthnChallenge
        {
            Key = key,
            Type = "register",
            OptionsJson = options.ToJson(),
            ExpiresAt = DateTime.UtcNow.Add(Ttl)
        });
        await _db.SaveChangesAsync();
    }

    public async Task<CredentialCreateOptions?> TakeRegisterOptionsAsync(string key)
    {
        var row = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Key == key && c.Type == "register" && c.ExpiresAt > DateTime.UtcNow);
        if (row is null) return null;
        _db.Challenges.Remove(row);
        await _db.SaveChangesAsync();
        return CredentialCreateOptions.FromJson(row.OptionsJson);
    }

    public async Task StoreAssertOptionsAsync(string key, AssertionOptions options)
    {
        await PurgeExpiredAsync();
        var existing = await _db.Challenges
            .Where(c => c.Key == key && c.Type == "assert")
            .ToListAsync();
        _db.Challenges.RemoveRange(existing);
        _db.Challenges.Add(new WebAuthnChallenge
        {
            Key = key,
            Type = "assert",
            OptionsJson = options.ToJson(),
            ExpiresAt = DateTime.UtcNow.Add(Ttl)
        });
        await _db.SaveChangesAsync();
    }

    public async Task<AssertionOptions?> TakeAssertOptionsAsync(string key)
    {
        var row = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Key == key && c.Type == "assert" && c.ExpiresAt > DateTime.UtcNow);
        if (row is null) return null;
        _db.Challenges.Remove(row);
        await _db.SaveChangesAsync();
        return AssertionOptions.FromJson(row.OptionsJson);
    }

    private async Task PurgeExpiredAsync()
    {
        var expired = await _db.Challenges
            .Where(c => c.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();
        if (expired.Count > 0)
        {
            _db.Challenges.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }
}

