namespace AuthTest.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool MustChangePassword { get; set; } = true;
    public ICollection<WebAuthnCredential> Credentials { get; set; } = new List<WebAuthnCredential>();
}
