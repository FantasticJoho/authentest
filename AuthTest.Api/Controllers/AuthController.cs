using AuthTest.Api.Data;
using AuthTest.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SessionStore _sessions;

    public AuthController(AppDbContext db, SessionStore sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] CheckRequest req)
    {
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null)
            return Ok(new CheckResponse(false, false, false));

        return Ok(new CheckResponse(true, user.Credentials.Any(), user.MustChangePassword));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Ok(new LoginResponse(false, null, false, "Invalid credentials"));

        var token = _sessions.CreateSession(user.Id, user.Credentials.Any());
        return Ok(new LoginResponse(true, token, user.MustChangePassword, null));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var session = _sessions.GetSession(req.Token);
        if (session is null)
            return Unauthorized(new { error = "Invalid session" });

        var user = await _db.Users.FindAsync(session.UserId);
        if (user is null)
            return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, 12);
        user.MustChangePassword = false;
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

public record CheckRequest(string Username);
public record CheckResponse(bool Exists, bool HasWebAuthn, bool MustChangePassword);
public record LoginRequest(string Username, string Password);
public record LoginResponse(bool Success, string? Token, bool MustChangePassword, string? Error);
public record ChangePasswordRequest(string Token, string NewPassword);
