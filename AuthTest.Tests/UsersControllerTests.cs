using AuthTest.Api.Controllers;
using AuthTest.Api.Data;
using AuthTest.Api.Models;
using AuthTest.Api.Services;
using AuthTest.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AuthTest.Tests;

public class UsersControllerTests
{
    private static (UsersController ctrl, AppDbContext db) Build()
    {
        var db = DbHelper.CreateContext();
        return (new UsersController(db), db);
    }

    // ── List ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAllUsers()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User { Username = "alice", PasswordHash = "x" });
        db.Users.Add(new User { Username = "bob", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var result = (OkObjectResult)await ctrl.List();
        var users = (IEnumerable<UserDto>)result.Value!;
        Assert.Equal(2, users.Count());
    }

    [Fact]
    public async Task List_EmptyDb_ReturnsEmpty()
    {
        var (ctrl, _) = Build();
        var result = (OkObjectResult)await ctrl.List();
        var users = (IEnumerable<UserDto>)result.Value!;
        Assert.Empty(users);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NewUser_ReturnsCreated()
    {
        var (ctrl, db) = Build();
        var result = await ctrl.Create(new CreateUserRequest("alice", "pass"));
        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(1, db.Users.Count());
    }

    [Fact]
    public async Task Create_DuplicateUsername_ReturnsConflict()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User { Username = "alice", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var result = await ctrl.Create(new CreateUserRequest("alice", "pass"));
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Create_DuplicateUsernameIsCaseInsensitive()
    {
        var (ctrl, db) = Build();
        db.Users.Add(new User { Username = "Alice", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var result = await ctrl.Create(new CreateUserRequest("alice", "pass"));
        Assert.IsType<ConflictObjectResult>(result);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingUser_ReturnsNoContent()
    {
        var (ctrl, db) = Build();
        var user = new User { Username = "alice", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await ctrl.Delete(user.Id);
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, db.Users.Count());
    }

    [Fact]
    public async Task Delete_NonExistentUser_ReturnsNotFound()
    {
        var (ctrl, _) = Build();
        var result = await ctrl.Delete(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result);
    }

    // ── Reset2Fa ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Reset2Fa_RemovesAllCredentials()
    {
        var (ctrl, db) = Build();
        var user = new User { Username = "alice", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Credentials.Add(new WebAuthnCredential
        {
            UserId = user.Id,
            Name = "Key1",
            CredentialId = new byte[] { 1, 2, 3 },
            PublicKey = new byte[] { 4, 5, 6 },
            SignCount = 0,
            AaGuid = ""
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Reset2Fa(user.Id);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, db.Credentials.Count());
    }

    [Fact]
    public async Task Reset2Fa_NonExistentUser_ReturnsNotFound()
    {
        var (ctrl, _) = Build();
        var result = await ctrl.Reset2Fa(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result);
    }
}
