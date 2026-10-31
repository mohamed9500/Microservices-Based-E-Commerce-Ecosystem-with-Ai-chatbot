using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nexus.BuildingBlocks.Auth;
using Nexus.Identity.Domain;

namespace Nexus.Identity.Services;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresAtUtc) Create(User user)
    {
        var o = options.Value;
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("name", user.FullName),
            new Claim("role", user.Role),
            new Claim("jti", Guid.NewGuid().ToString())
        };
        var expires = DateTime.UtcNow.AddMinutes(o.ExpiryMinutes);
        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, notBefore: DateTime.UtcNow, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
