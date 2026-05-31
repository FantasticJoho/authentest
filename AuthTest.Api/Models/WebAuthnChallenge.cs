namespace AuthTest.Api.Models;

public class WebAuthnChallenge
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";       // "register" | "assert"
    public string OptionsJson { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
