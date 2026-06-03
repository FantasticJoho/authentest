using AuthTest.Api.Data;
using AuthTest.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _db.Users
            .Include(u => u.Credentials)
            .Select(u => new UserDto(u.Id, u.Username, u.Credentials.Any(), u.MustChangePassword))
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == req.Username.ToLower()))
            return Conflict(new { error = "Username already taken" });

        var user = new User
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 12),
            MustChangePassword = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new UserDto(user.Id, user.Username, false, true));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-2fa")]
    public async Task<IActionResult> Reset2Fa(Guid id)
    {
        var user = await _db.Users.Include(u => u.Credentials).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        _db.Credentials.RemoveRange(user.Credentials);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

public record CreateUserRequest(string Username, string Password);
public record UserDto(Guid Id, string Username, bool HasWebAuthn, bool MustChangePassword);
