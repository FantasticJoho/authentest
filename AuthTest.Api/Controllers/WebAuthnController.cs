using AuthTest.Api.Data;
using AuthTest.Api.Models;
using AuthTest.Api.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
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
    private readonly IFido2 _fido2;

    public WebAuthnController(AppDbContext db, SessionStore sessions, ChallengeStore challenges, IFido2 fido2)
    {
        _db = db;
        _sessions = sessions;
        _challenges = challenges;
        _fido2 = fido2;
    }

    [HttpPost("register/begin")]
    public async Task<IActionResult> RegisterBegin([FromBody] RegisterBeginRequest req)
    {
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

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None
        });

        _challenges.StoreRegisterOptions(req.Token, options);

        return Ok(options);
    }

    [HttpPost("register/complete")]
    public async Task<IActionResult> RegisterComplete([FromBody] RegisterCompleteRequest req)
    {
        var session = _sessions.GetSession(req.Token);
        if (session is null) return Unauthorized(new { error = "Invalid session" });

        if (string.IsNullOrWhiteSpace(req.KeyName))
            return BadRequest(new { error = "KeyName is required" });

        var storedOptions = _challenges.TakeRegisterOptions(req.Token);
        if (storedOptions is null) return BadRequest(new { error = "No pending challenge" });

        var user = await _db.Users.FindAsync(session.UserId);
        if (user is null) return NotFound();

        IsCredentialIdUniqueToUserAsyncDelegate isUniqueCallback = async (credIdParams, ct) =>
            !await _db.Credentials.AnyAsync(c => c.CredentialId.SequenceEqual(credIdParams.CredentialId), ct);

        try
        {
            var makeResult = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
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
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null || !user.Credentials.Any())
            return BadRequest(new { error = "No WebAuthn credentials found" });

        var allowedKeys = user.Credentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedKeys,
            UserVerification = UserVerificationRequirement.Preferred
        });

        _challenges.StoreAssertOptions($"auth:{req.Username.ToLower()}", options);

        return Ok(options);
    }

    [HttpPost("authenticate/complete")]
    public async Task<IActionResult> AuthenticateComplete([FromBody] AuthCompleteRequest req)
    {
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null)
            return BadRequest(new { error = "User not found" });

        var storedOptions = _challenges.TakeAssertOptions($"auth:{req.Username.ToLower()}");
        if (storedOptions is null)
            return BadRequest(new { error = "No pending challenge" });

        var credential = user.Credentials
            .FirstOrDefault(c => c.CredentialId.SequenceEqual(req.AssertionResponse.RawId));

        if (credential is null)
            return BadRequest(new { error = "Credential not found" });

        IsUserHandleOwnerOfCredentialIdAsync isUserOwner = async (p, ct) =>
            await _db.Credentials.AnyAsync(
                c => c.CredentialId.SequenceEqual(p.CredentialId) && c.UserId == user.Id, ct);

        try
        {
            var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = req.AssertionResponse,
                OriginalOptions = storedOptions,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = isUserOwner
            });

            credential.SignCount = result.SignCount;
            await _db.SaveChangesAsync();

            var token = _sessions.CreateSession(user.Id, true);
            return Ok(new { success = true, token });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record RegisterBeginRequest(string Token);
public record RegisterCompleteRequest(string Token, string KeyName, AuthenticatorAttestationRawResponse AttestationResponse);
public record AuthBeginRequest(string Username);
public record AuthCompleteRequest(string Username, AuthenticatorAssertionRawResponse AssertionResponse);
