using BuffCore.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BuffCore.Data
{
    /// <summary>
    /// Dependency injection registrations for BuffCore.Data.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a <see cref="DbContext"/> with provider-neutral conventions applied:
        /// the migrations assembly is set to the context's own assembly and <see cref="DatabaseConfig.CommandTimeout"/>
        /// is enforced. The database engine itself is chosen by the host through <paramref name="configureProvider"/>,
        /// keeping this package independent of any specific provider (Npgsql, SQL Server, SQLite, ...).
        /// </summary>
        /// <typeparam name="TDbContext">The host's <see cref="AuditDbContext"/> (or <see cref="DbContext"/>) type.</typeparam>
        /// <param name="services">Service collection to register the context into.</param>
        /// <param name="databaseConfig">Connection settings read by the host from its own configuration.</param>
        /// <param name="configureProvider">Delegate that selects and configures the EF Core provider, e.g. <c>x => x.UseNpgsql(...)</c>.</param>
        /// <returns>The updated <see cref="IServiceCollection"/> instance for chaining.</returns>
        public static IServiceCollection AddBuffCoreData<TDbContext>(
            this IServiceCollection services,
            DatabaseConfig databaseConfig,
            Action<DbContextOptionsBuilder> configureProvider)
            where TDbContext : DbContext
        {
            var contextAssemblyName = typeof(TDbContext).Assembly.GetName().Name;

            // Fail fast at registration time when the host forgot to choose a provider,
            // instead of surfacing the error on first context resolution.
            var probe = new DbContextOptionsBuilder<TDbContext>();
            configureProvider(probe);
            if (!probe.Options.Extensions.OfType<RelationalOptionsExtension>().Any())
            {
                throw new InvalidOperationException(
                    "No relational provider was configured by the host delegate. " +
                    "Call a provider inside configureProvider, e.g. x => x.UseNpgsql(connectionString).");
            }

            services.AddDbContext<TDbContext>(x =>
            {
                // The host delegate selects the provider first, so the relational extension exists
                // below and the generic conventions can be appended onto it.
                configureProvider(x);

                var relationalExtension = x.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
                if (relationalExtension == null)
                {
                    throw new InvalidOperationException(
                        "No relational provider was configured by the host delegate. " +
                        "Call a provider inside configureProvider, e.g. x => x.UseNpgsql(connectionString).");
                }

                ((IDbContextOptionsBuilderInfrastructure)x).AddOrUpdateExtension(
                    relationalExtension
                        .WithMigrationsAssembly(contextAssemblyName)
                        .WithCommandTimeout(databaseConfig.CommandTimeout));
            });

            return services;
        }
    }
}
