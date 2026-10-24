using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Nexus.BuildingBlocks.Web;

namespace Nexus.BuildingBlocks.Auth;

public static class AuthExtensions
{
    public static IServiceCollection AddNexusAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var opts = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(opts.Key) || Encoding.UTF8.GetByteCount(opts.Key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured or shorter than 32 bytes. Set the environment variable Jwt__Key (see .env.example).");
        }

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // keep "sub", "role", "email", "name" as issued
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = opts.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        await ProblemWriter.WriteAsync(ctx.HttpContext, 401, "Unauthorized",
                            "Authentication is required or the access token is invalid or expired.");
                    },
                    OnForbidden = ctx => ProblemWriter.WriteAsync(ctx.HttpContext, 403, "Forbidden",
                        "You do not have permission to perform this action.")
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy("admin", p => p.RequireRole(Roles.Admin));
            o.AddPolicy("authenticated", p => p.RequireAuthenticatedUser());
        });
        return services;
    }
}

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal p)
    {
        var v = p.FindFirst("sub")?.Value;
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedException();
    }

    public static Guid? TryGetUserId(this ClaimsPrincipal p) =>
        Guid.TryParse(p.FindFirst("sub")?.Value, out var id) ? id : null;

    public static string GetEmail(this ClaimsPrincipal p) => p.FindFirst("email")?.Value ?? "";
    public static string GetDisplayName(this ClaimsPrincipal p) => p.FindFirst("name")?.Value ?? "Customer";
    public static bool IsAdmin(this ClaimsPrincipal p) => p.IsInRole(Roles.Admin);
}
