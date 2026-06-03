using System.Text.Json;
using AuthTest.Api.Data;
using AuthTest.Api.Models;
using AuthTest.Api.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Api.Controllers;

[ApiController]
[Route("webauthn")]
public class WebAuthnController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SessionStore _sessions;
    private readonly ChallengeStore _challenges;
    private readonly IConfiguration _configuration;

    public WebAuthnController(AppDbContext db, SessionStore sessions, ChallengeStore challenges, IConfiguration configuration)
    {
        _db = db;
        _sessions = sessions;
        _challenges = challenges;
        _configuration = configuration;
    }

    // WebAuthn depends on RP ID (domain) and authorized origins.
    // We build the Fido2 instance per request to keep these values aligned with the current browser flow.
    private IFido2 CreateFido2(string? rpId)
    {
        var effectiveRpId = string.IsNullOrWhiteSpace(rpId)
            ? (_configuration["Fido2:ServerDomain"] ?? "localhost")
            : rpId;
        var configuredOrigins = _configuration.GetSection("Fido2:Origins").Get<string[]>() ?? Array.Empty<string>();
        var origins = new HashSet<string>(configuredOrigins, StringComparer.OrdinalIgnoreCase);

        return new Fido2(new Fido2Configuration
        {
            ServerDomain = effectiveRpId,
            ServerName = _configuration["Fido2:ServerName"] ?? "AuthTest",
            Origins = origins
        });
    }

    [HttpPost("register/begin")]
    public async Task<IActionResult> RegisterBegin([FromBody] RegisterBeginRequest req)
    {
        // PHASE 1 (enrollment): prepare CredentialCreateOptions and a fresh challenge.
        // The challenge is stored server-side and must be returned unchanged in /register/complete.
        var fido2 = CreateFido2(req.RpId);
        var session = _sessions.GetSession(req.Token);
        if (session is null) return Unauthorized(new { error = "Invalid session" });

        var user = await _db.Users.FindAsync(session.UserId);
        if (user is null) return NotFound();

        var fidoUser = new Fido2User
        {
            Id = user.Id.ToByteArray(),
            Name = user.Username,
            DisplayName = user.Username
        };

        var existingKeys = await _db.Credentials
            .Where(c => c.UserId == user.Id)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync();

        var authenticatorSelection = new AuthenticatorSelection
        {
            UserVerification = UserVerificationRequirement.Preferred,
            AuthenticatorAttachment = AuthenticatorAttachment.Platform
        };

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None
        });

        // Challenge is temporary (TTL managed by ChallengeStore).
        await _challenges.StoreRegisterOptionsAsync(req.Token, options);

        return Ok(options);
    }

    [HttpPost("register/complete")]
    public async Task<IActionResult> RegisterComplete([FromBody] RegisterCompleteRequest req)
    {
        // PHASE 2 (enrollment): verify attestation with the exact options/challenge from phase 1,
        // then persist the new credential public data.
        var fido2 = CreateFido2(req.RpId);
        var session = _sessions.GetSession(req.Token);
        if (session is null) return Unauthorized(new { error = "Invalid session" });

        if (string.IsNullOrWhiteSpace(req.KeyName))
            return BadRequest(new { error = "KeyName is required" });

        var storedOptions = await _challenges.TakeRegisterOptionsAsync(req.Token);
        if (storedOptions is null) return BadRequest(new { error = "No pending challenge" });

        var user = await _db.Users.FindAsync(session.UserId);
        if (user is null) return NotFound();

        // Enforces that a credential ID cannot be registered twice.
        IsCredentialIdUniqueToUserAsyncDelegate isUniqueCallback = async (credIdParams, ct) =>
            !await _db.Credentials.AnyAsync(c => c.CredentialId.SequenceEqual(credIdParams.CredentialId), ct);

        try
        {
            var makeResult = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = req.AttestationResponse,
                OriginalOptions = storedOptions,
                IsCredentialIdUniqueToUserCallback = isUniqueCallback
            });

            var credential = new WebAuthnCredential
            {
                UserId = session.UserId,
                Name = req.KeyName,
                CredentialId = makeResult.Id,
                PublicKey = makeResult.PublicKey,
                SignCount = 0,
                AaGuid = string.Empty
            };

            _db.Credentials.Add(credential);
            await _db.SaveChangesAsync();

            // Marks this session as WebAuthn-enrolled so UI can unlock protected screens.
            _sessions.MarkEnrolled(req.Token);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("authenticate/begin")]
    public async Task<IActionResult> AuthenticateBegin([FromBody] AuthBeginRequest req)
    {
        // PHASE 1 (authentication): prepare AssertionOptions and a fresh challenge.
        // AllowedCredentials scopes the assertion to credentials known for this user.
        var fido2 = CreateFido2(req.RpId);
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null || !user.Credentials.Any())
            return BadRequest(new { error = "No WebAuthn credentials found" });

        var allowedKeys = user.Credentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedKeys,
            UserVerification = UserVerificationRequirement.Preferred
        });

        // Store challenge/options; /authenticate/complete will consume and remove them.
        await _challenges.StoreAssertOptionsAsync($"auth:{req.Username.ToLower()}", options);

        return Ok(options);
    }

    [HttpPost("authenticate/complete")]
    public async Task<IActionResult> AuthenticateComplete([FromBody] AuthCompleteRequest req)
    {
        // PHASE 2 (authentication): verify signed assertion against
        // challenge (anti-replay), origin/rp (anti-phishing), and stored public key.
        var fido2 = CreateFido2(req.RpId);
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null)
            return BadRequest(new { error = "User not found" });

        var storedOptions = await _challenges.TakeAssertOptionsAsync($"auth:{req.Username.ToLower()}");
        if (storedOptions is null)
            return BadRequest(new { error = "No pending challenge" });

        AuthenticatorAssertionRawResponse assertionResponse;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
                req.AssertionResponse.GetRawText(), opts)!;
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to parse assertion: " + ex.Message });
        }

        if (assertionResponse?.RawId is null)
            return BadRequest(new { error = "AssertionResponse.RawId is missing" });

        var credential = user.Credentials
            .FirstOrDefault(c => c.CredentialId.SequenceEqual(assertionResponse.RawId));

        if (credential is null)
            return BadRequest(new { error = "Credential not found" });

        // Confirms the credential really belongs to this user.
        IsUserHandleOwnerOfCredentialIdAsync isUserOwner = async (p, ct) =>
            await _db.Credentials.AnyAsync(
                c => c.CredentialId.SequenceEqual(p.CredentialId) && c.UserId == user.Id, ct);

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = storedOptions,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = isUserOwner
            });

            // Persist updated signature counter to help detect cloned authenticators.
            credential.SignCount = result.SignCount;
            await _db.SaveChangesAsync();

            // Create a standard app session token after successful WebAuthn verification.
            var token = _sessions.CreateSession(user.Id, true);
            return Ok(new { success = true, token });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record RegisterBeginRequest(string Token, string? RpId);
public record RegisterCompleteRequest(string Token, string KeyName, AuthenticatorAttestationRawResponse AttestationResponse, string? RpId);
public record AuthBeginRequest(string Username, string? RpId);
public record AuthCompleteRequest(string Username, JsonElement AssertionResponse, string? RpId);
