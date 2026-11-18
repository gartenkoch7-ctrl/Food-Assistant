using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>PostgreSQL via EF Core and migrations at startup (ADR-0007).</summary>
public static class PersistenceRegistration
{
    /// <summary>Configuration key of the connection string (env: <c>ConnectionStrings__FoodAssistant</c>).</summary>
    public const string ConnectionStringName = "FoodAssistant";

    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The connection string is read when the first context is created, not here, so test
        // hosts can provide it late and a missing value surfaces as a clear startup failure.
        builder.Services.AddDbContext<AppDbContext>((services, options) =>
        {
            var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"No database configured. Set ConnectionStrings__{ConnectionStringName} (see appsettings.json).");
            }

            options.UseNpgsql(connectionString);
        });

        builder.Services.AddHostedService<DatabaseMigrationService>();
        return builder;
    }
}
