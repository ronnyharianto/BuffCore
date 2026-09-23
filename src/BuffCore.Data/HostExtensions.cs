using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuffCore.Data
{
    /// <summary>
    /// Startup helpers for applying database migrations when a host boots.
    /// </summary>
    public static class HostExtensions
    {
        /// <summary>
        /// Creates a service scope, resolves the host's <see cref="DbContext"/>, and applies any pending
        /// migrations so the database schema is up to date on every application start.
        /// Optionally also ensures the database exists (useful for provider-local development databases).
        /// </summary>
        /// <typeparam name="TDbContext">The host's <see cref="DbContext"/> type to migrate.</typeparam>
        /// <param name="host">The host whose services provide the context.</param>
        /// <param name="ensureCreated">
        /// When true, additionally calls <c>EnsureCreated</c>. Note this is only meaningful for databases
        /// created outside of migrations; with migrations in place it is redundant but harmless.
        /// </param>
        /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
        /// <returns>The same <see cref="IHost"/> instance for chaining.</returns>
        public static async Task<IHost> MigrateDatabaseAsync<TDbContext>(
            this IHost host,
            bool ensureCreated = false,
            CancellationToken cancellationToken = default)
            where TDbContext : DbContext
        {
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);

            if (ensureCreated)
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(typeof(HostExtensions));
            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                logger.LogInformation("Database migration completed for {DbContextType}.", typeof(TDbContext).Name);
            }

            return host;
        }
    }
}
