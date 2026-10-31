using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Nexus.Tests.Support;

/// <summary>
/// Configures the services for in-process testing through environment variables (they are visible to
/// WebApplication.CreateBuilder at startup): in-memory EF provider, no RabbitMQ, a test-only JWT key.
/// </summary>
public static class TestEnv
{
    public const string JwtKey = "unit-test-signing-key-0123456789-abcdef-NOT-A-SECRET";
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin-Test-Pass1";
    private static readonly object Gate = new();
    private static bool _applied;

    public static void Apply()
    {
        lock (Gate)
        {
            if (_applied) return;
            Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
            Environment.SetEnvironmentVariable("Database__UseInMemory", "true");
            Environment.SetEnvironmentVariable("Messaging__Enabled", "false");
            Environment.SetEnvironmentVariable("Admin__Email", AdminEmail);
            Environment.SetEnvironmentVariable("Admin__Password", AdminPassword);
            Environment.SetEnvironmentVariable("Services__Catalog", "http://localhost:1");
            Environment.SetEnvironmentVariable("Services__Inventory", "http://localhost:1");
            Environment.SetEnvironmentVariable("Services__Reviews", "http://localhost:1");
            Environment.SetEnvironmentVariable("Services__Cart", "http://localhost:1");
            Environment.SetEnvironmentVariable("Ai__Provider", "none");
            Environment.SetEnvironmentVariable("Ai__ApiKey", "");
            _applied = true;
        }
    }
}

public sealed class ApiFactory<TMarker> : WebApplicationFactory<TMarker> where TMarker : class
{
    public ApiFactory() { TestEnv.Apply(); }
}

public static class TestTokens
{
    public static string Create(string role, Guid? userId = null, string email = "user@test.local", string name = "Test User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestEnv.JwtKey));
        var claims = new[]
        {
            new Claim("sub", (userId ?? Guid.NewGuid()).ToString()), new Claim("email", email),
            new Claim("name", name), new Claim("role", role)
        };
        var jwt = new JwtSecurityToken("nexus-identity", "nexus-clients", claims, expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
