using BuffCore.Abstractions;
using BuffCore.Data.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace BuffCore.Data
{
    /// <summary>
    /// Base <see cref="DbContext"/> that automatically tracks audit fields such as Created, CreatedBy, Modified, and ModifiedBy
    /// on entities deriving from <see cref="EntityBase"/>.
    /// It also maps entities to database schemas dynamically based on the <see cref="DatabaseSchemaAttribute"/>,
    /// defaulting to the 'public' schema when the attribute is absent.
    /// </summary>
    /// <remarks>
    /// The acting user is supplied via the <see cref="CurrentUserAccessor"/> from BuffCore.Abstractions,
    /// which hosts populate per request (for example from authentication middleware). When no accessor is
    /// provided, audit stamps are written with an empty actor string.
    /// </remarks>
    public class AuditDbContext(DbContextOptions _options, CurrentUserAccessor? _currentUserAccessor = null, ILogger? _logger = null) : DbContext(_options)
    {
        /// <summary>
        /// Saves all changes made in this context to the database.
        /// Automatically sets audit fields for added and modified entities before saving.
        /// </summary>
        /// <returns>The number of state entries written to the database.</returns>
        public override int SaveChanges()
        {
            UpdateActorAndTimestamps();

            _logger?.LogInformation("Saving changes to database with audit fields updated.");

            return base.SaveChanges();
        }

        /// <summary>
        /// Asynchronously saves all changes made in this context to the database.
        /// Automatically sets audit fields for added and modified entities before saving.
        /// </summary>
        /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateActorAndTimestamps();

            _logger?.LogInformation("Saving changes asynchronously to database with audit fields updated.");

            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Updates audit properties (Created, CreatedBy, Modified, ModifiedBy) on entities tracked by this context.
        /// Sets Created and CreatedBy for new entities, and Modified and ModifiedBy for updated entities.
        /// </summary>
        private void UpdateActorAndTimestamps()
        {
            var actor = _currentUserAccessor?.UserId.ToString() ?? string.Empty;

            var createdCount = 0;
            var modifiedCount = 0;

            // Single pass: stamp audit fields and count in the same enumeration, so no
            // ChangeTracker re-enumeration happens purely for logging when Debug is disabled.
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is not EntityBase entity)
                {
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        entity.Created = DateTime.UtcNow;
                        entity.CreatedBy = actor;
                        createdCount++;
                        break;

                    case EntityState.Modified:
                        entity.Modified = DateTime.UtcNow;
                        entity.ModifiedBy = actor;
                        modifiedCount++;
                        break;
                }
            }

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Updated audit fields: {CreatedCount} created, {ModifiedCount} modified entries.",
                    createdCount, modifiedCount);
            }
        }

        /// <summary>
        /// Configures the model and applies database schema mappings based on <see cref="DatabaseSchemaAttribute"/>.
        /// Defaults to the 'public' schema if none is specified.
        /// </summary>
        /// <param name="modelBuilder">Builder used to construct the model for the context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var dbSetProperties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                           .Where(p =>
                                               p.PropertyType.IsGenericType &&
                                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

            foreach (var dbSetProperty in dbSetProperties)
            {
                var entityType = dbSetProperty.PropertyType.GenericTypeArguments[0];
                var schemaAttribute = entityType.GetCustomAttributes(typeof(DatabaseSchemaAttribute), false).FirstOrDefault() as DatabaseSchemaAttribute;
                var schema = schemaAttribute?.Schema ?? "public";

                var tableName = dbSetProperty.Name;

                modelBuilder.Entity(entityType).ToTable(tableName, schema);
            }

            _logger?.LogInformation("Configured database schema mappings for entities.");
        }
    }
}
