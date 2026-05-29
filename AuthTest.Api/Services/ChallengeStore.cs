using System.Collections.Concurrent;
using Fido2NetLib;

namespace AuthTest.Api.Services;

public class ChallengeStore
{
    private readonly ConcurrentDictionary<string, CredentialCreateOptions> _registerOptions = new();
    private readonly ConcurrentDictionary<string, AssertionOptions> _assertOptions = new();

    public void StoreRegisterOptions(string key, CredentialCreateOptions options) =>
        _registerOptions[key] = options;

    public CredentialCreateOptions? TakeRegisterOptions(string key)
    {
        _registerOptions.TryRemove(key, out var opts);
        return opts;
    }

    public void StoreAssertOptions(string key, AssertionOptions options) =>
        _assertOptions[key] = options;

    public AssertionOptions? TakeAssertOptions(string key)
    {
        _assertOptions.TryRemove(key, out var opts);
        return opts;
    }
}
