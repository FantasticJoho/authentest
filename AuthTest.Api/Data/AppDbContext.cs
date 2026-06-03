using Microsoft.EntityFrameworkCore;
using AuthTest.Api.Models;

namespace AuthTest.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<WebAuthnCredential> Credentials => Set<WebAuthnCredential>();
    public DbSet<WebAuthnChallenge> Challenges => Set<WebAuthnChallenge>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<WebAuthnCredential>()
            .HasOne(c => c.User)
            .WithMany(u => u.Credentials)
            .HasForeignKey(c => c.UserId);
    }
}
