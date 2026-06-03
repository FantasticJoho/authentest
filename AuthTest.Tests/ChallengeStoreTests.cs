using AuthTest.Api.Models;
using AuthTest.Api.Services;
using AuthTest.Tests.Helpers;
using Xunit;

namespace AuthTest.Tests;

public class ChallengeStoreTests
{
    // ── register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAndTakeRegisterOptions_ReturnsOptions()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);
        var json = RegisterJson();

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "tok1",
            Type = "register",
            OptionsJson = json,
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        });
        await db.SaveChangesAsync();

        var result = await store.TakeRegisterOptionsAsync("tok1");

        Assert.NotNull(result);
        Assert.Null(await store.TakeRegisterOptionsAsync("tok1")); // consumed
    }

    [Fact]
    public async Task TakeRegisterOptions_MissingKey_ReturnsNull()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        var result = await store.TakeRegisterOptionsAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task TakeRegisterOptions_ExpiredChallenge_ReturnsNull()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "expired",
            Type = "register",
            OptionsJson = RegisterJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var result = await store.TakeRegisterOptionsAsync("expired");

        Assert.Null(result);
    }

    [Fact]
    public async Task TakeRegisterOptions_RemovesRow()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "tok2",
            Type = "register",
            OptionsJson = RegisterJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        });
        await db.SaveChangesAsync();

        _ = await store.TakeRegisterOptionsAsync("tok2");

        Assert.Equal(0, db.Challenges.Count());
    }

    // ── assert ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAndTakeAssertOptions_ReturnsOptions()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "auth:bob",
            Type = "assert",
            OptionsJson = AssertJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        });
        await db.SaveChangesAsync();

        var result = await store.TakeAssertOptionsAsync("auth:bob");

        Assert.NotNull(result);
        Assert.Null(await store.TakeAssertOptionsAsync("auth:bob")); // consumed
    }

    [Fact]
    public async Task TakeAssertOptions_ExpiredChallenge_ReturnsNull()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "auth:expired",
            Type = "assert",
            OptionsJson = AssertJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var result = await store.TakeAssertOptionsAsync("auth:expired");

        Assert.Null(result);
    }

    // ── StoreRegisterOptionsAsync cleans up expired ───────────────────────

    [Fact]
    public async Task StoreRegisterOptions_PurgesExpiredRows()
    {
        using var db = DbHelper.CreateContext();
        var store = new ChallengeStore(db);

        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "stale",
            Type = "register",
            OptionsJson = RegisterJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        // Storing a new challenge triggers purge of expired rows
        db.Challenges.Add(new WebAuthnChallenge
        {
            Key = "fresh",
            Type = "register",
            OptionsJson = RegisterJson(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        });
        await db.SaveChangesAsync();

        // Trigger PurgeExpiredAsync via a Store call
        await store.StoreRegisterOptionsAsync("another", Fido2NetLib.CredentialCreateOptions.FromJson(RegisterJson()));

        Assert.DoesNotContain(db.Challenges.ToList(), c => c.Key == "stale");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string RegisterJson() =>
        """{"status":"ok","errorMessage":"","rp":{"name":"AuthTest","id":"localhost"},"user":{"name":"test","id":"AQIDBA==","displayName":"test"},"challenge":"AQIDBA==","pubKeyCredParams":[{"type":"public-key","alg":-7}],"timeout":60000,"excludeCredentials":[],"attestation":"none","extensions":{}}""";

    private static string AssertJson() =>
        """{"status":"ok","errorMessage":"","challenge":"AQIDBA==","timeout":60000,"rpId":"localhost","allowCredentials":[],"userVerification":"preferred","extensions":{}}""";
}
