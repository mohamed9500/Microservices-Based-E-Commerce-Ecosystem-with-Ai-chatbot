using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Nexus.BuildingBlocks.Auth;

namespace Nexus.BuildingBlocks.Web;

public static class NexusWebExtensions
{
    public static WebApplicationBuilder AddNexusDefaults(this WebApplicationBuilder builder, string serviceTitle)
    {
        // Structured (JSON) logging with scopes (correlation id).
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });

        builder.Services.AddNexusAuthentication(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationHandler>();
        builder.Services.Configure<JsonOptions>(o =>
        {
            foreach (var c in JsonDefaults.Options.Converters) o.SerializerOptions.Converters.Add(c);
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = serviceTitle, Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
                BearerFormat = "JWT", In = ParameterLocation.Header,
                Description = "Paste the JWT access token returned by /api/auth/login."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddHealthChecks();

        // Production-safe CORS: only explicitly configured origins are allowed (none by default).
        var origins = (builder.Configuration["Cors:Origins"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        builder.Services.AddCors(o => o.AddPolicy("default", p =>
        {
            if (origins.Length > 0)
                p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders(CorrelationMiddleware.Header);
        }));
        return builder;
    }

    public static WebApplication UseNexusDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseCors("default");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        return app;
    }
}
