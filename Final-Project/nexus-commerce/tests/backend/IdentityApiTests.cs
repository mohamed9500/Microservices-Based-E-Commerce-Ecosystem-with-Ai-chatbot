using System.Net;
using System.Net.Http.Json;
using Nexus.Identity;
using Nexus.Tests.Support;

namespace Nexus.Tests;

public class IdentityApiTests(ApiFactory<IdentityApiMarker> factory) : IClassFixture<ApiFactory<IdentityApiMarker>>
{
    private sealed record AuthResult(string AccessToken, UserResult User);
    private sealed record UserResult(Guid Id, string Email, string Role);

    private HttpClient Anonymous() => factory.CreateClient();
    private HttpClient As(string token) => factory.CreateClient().WithBearer(token);

    [Fact]
    public async Task Register_then_login_then_read_profile()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        var reg = await Anonymous().PostAsJsonAsync("/api/auth/register", new { email, password = "Passw0rdOK", fullName = "Test Customer" });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        Assert.Equal("Customer", (await reg.Content.ReadFromJsonAsync<AuthResult>())!.User.Role);

        var login = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rdOK" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken;

        Assert.Equal(HttpStatusCode.OK, (await As(token).GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_with_conflict()
    {
        var body = new { email = $"dup{Guid.NewGuid():N}@test.local", password = "Passw0rdOK", fullName = "Dup User" };
        Assert.Equal(HttpStatusCode.Created, (await Anonymous().PostAsJsonAsync("/api/auth/register", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Anonymous().PostAsJsonAsync("/api/auth/register", body)).StatusCode);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task Weak_passwords_are_rejected(string password)
    {
        var res = await Anonymous().PostAsJsonAsync("/api/auth/register", new { email = $"w{Guid.NewGuid():N}@test.local", password, fullName = "Weak User" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_login_returns_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.local", password = "Whatever123" })).StatusCode);

    [Fact]
    public async Task Profile_requires_authentication() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/api/profile")).StatusCode);

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var token = TestTokens.Create("Admin");
        var tampered = token[..^3] + (token.EndsWith("AAA") ? "BBB" : "AAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await As(tampered).GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_user_list_is_forbidden_for_customers_and_open_to_admins()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(TestTokens.Create("Customer")).GetAsync("/api/admin/users")).StatusCode);

        // The development admin is created from Admin__Email / Admin__Password.
        var login = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = TestEnv.AdminEmail, password = TestEnv.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var adminToken = (await login.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken;
        Assert.Equal(HttpStatusCode.OK, (await As(adminToken).GetAsync("/api/admin/users")).StatusCode);
    }
}
