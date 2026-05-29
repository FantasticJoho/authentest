namespace AuthTest.Api.Models;

public class WebAuthnCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Name { get; set; } = default!;
    public byte[] CredentialId { get; set; } = default!;
    public byte[] PublicKey { get; set; } = default!;
    public uint SignCount { get; set; }
    public string AaGuid { get; set; } = string.Empty;
}
