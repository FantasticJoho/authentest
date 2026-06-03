using AuthTest.Api.Controllers;
using AuthTest.Api.Data;
using AuthTest.Api.Models;
using AuthTest.Api.Services;
using AuthTest.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AuthTest.Tests;

public class AuthControllerTests
{
    private static (AuthController ctrl, AppDbContext db) Build(string? dbName = null)
    {
        var db = DbHelper.CreateContext(dbName);
        return (new AuthController(db), db);
    }

    // ── Check ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_UnknownUser_ReturnsFalse()
    {
        var (ctrl, _) = Build();
        var result = (OkObjectResult)await ctrl.Check(new CheckRequest("nobody"));
        var body = (CheckResponse)result.Value!;
        Assert.False(body.Exists);
        Assert.False(body.HasWebAuthn);
    }

    [Fact]
    public async Task Check_KnownUser_NoWebAuthn_ReturnsExistsTrue_HasWebAuthnFalse()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User { Username = "alice", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var result = (OkObjectResult)await ctrl.Check(new CheckRequest("alice"));
        var body = (CheckResponse)result.Value!;
        Assert.True(body.Exists);
        Assert.False(body.HasWebAuthn);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsSuccess()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User
        {
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4)
        });
        await db.SaveChangesAsync();

        var result = (OkObjectResult)await ctrl.Login(new LoginRequest("alice", "secret"));
        var body = (LoginResponse)result.Value!;
        Assert.True(body.Success);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsFailure()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User
        {
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct", 4)
        });
        await db.SaveChangesAsync();

        var result = (OkObjectResult)await ctrl.Login(new LoginRequest("alice", "wrong"));
        var body = (LoginResponse)result.Value!;
        Assert.False(body.Success);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsFailure()
    {
        var (ctrl, _) = Build();
        var result = (OkObjectResult)await ctrl.Login(new LoginRequest("ghost", "x"));
        var body = (LoginResponse)result.Value!;
        Assert.False(body.Success);
    }

    // ── ChangePassword ────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidUser_UpdatesPasswordAndMustChangeFlag()
    {
        var db = DbHelper.CreateContext();
        var ctrl = new AuthController(db);

        var user = new User { Username = "bob", PasswordHash = BCrypt.Net.BCrypt.HashPassword("old", 4), MustChangePassword = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await ctrl.ChangePassword(new ChangePasswordRequest(user.Username, "newpass"));
        Assert.IsType<OkObjectResult>(result);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.False(updated!.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpass", updated.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_ReturnsNotFound()
    {
        var (ctrl, _) = Build();
        var result = await ctrl.ChangePassword(new ChangePasswordRequest("bad-user", "x"));
        Assert.IsType<NotFoundResult>(result);
    }
}
