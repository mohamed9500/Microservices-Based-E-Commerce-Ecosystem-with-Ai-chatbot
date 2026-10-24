using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexus.BuildingBlocks.Data;

public static class DbExtensions
{
    /// <summary>
    /// Registers a service-owned DbContext. SQL Server by default; the EF InMemory provider is used only when
    /// Database:UseInMemory=true AND the environment is Development/Testing (automated tests).
    /// </summary>
    public static IServiceCollection AddServiceDbContext<TContext>(this IServiceCollection services,
        IConfiguration config, string connectionName = "Default") where TContext : DbContext
    {
        if (config.GetValue<bool>("Database:UseInMemory"))
        {
            var name = $"{typeof(TContext).Name}-{Guid.NewGuid():N}";
            services.AddDbContext<TContext>(o => o.UseInMemoryDatabase(name));
        }
        else
        {
            var cs = config.GetConnectionString(connectionName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is not configured.");
            services.AddDbContext<TContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));
        }
        services.AddHealthChecks().AddDbContextCheck<TContext>("database");
        return services;
    }

    /// <summary>
    /// Creates the database/schema (EnsureCreated, with retries while SQL Server starts) and runs an optional seeder.
    /// No EF migrations are shipped: see docs/database.md for the rationale and how to move to migrations.
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(this WebApplication app,
        Func<TContext, IServiceProvider, Task>? seed = null, int maxAttempts = 30) where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"DbInit.{typeof(TContext).Name}");
        if (app.Configuration.GetValue<bool>("Database:UseInMemory") &&
            !(app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")))
            throw new InvalidOperationException("Database:UseInMemory is only allowed in Development/Testing.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                await db.Database.EnsureCreatedAsync();
                if (seed != null) await seed(db, scope.ServiceProvider);
                logger.LogInformation("Database initialised for {Context}", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(4));
            }
        }
    }
}
