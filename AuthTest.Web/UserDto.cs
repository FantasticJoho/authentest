namespace AuthTest.Web
{
    public class UserDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public bool HasWebAuthn { get; set; }
        public bool MustChangePassword { get; set; }
    }
}
