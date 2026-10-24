namespace Nexus.BuildingBlocks.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "nexus-identity";
    public string Audience { get; set; } = "nexus-clients";
    /// <summary>HMAC signing key. Provide via environment variable Jwt__Key (min 32 bytes). Never commit it.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
