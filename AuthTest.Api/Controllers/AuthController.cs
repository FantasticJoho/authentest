using AuthTest.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
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
            return Ok(new LoginResponse(false, false, "Invalid credentials"));

        return Ok(new LoginResponse(true, user.MustChangePassword, null));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());
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
public record LoginResponse(bool Success, bool MustChangePassword, string? Error);
public record ChangePasswordRequest(string Username, string NewPassword);
