using System.ComponentModel.DataAnnotations;

namespace BuffCore.Data
{
    /// <summary>
    /// Base class for all entities, providing common properties like Id, audit timestamps, and soft delete status.
    /// </summary>
    public class EntityBase
    {
        /// <summary>
        /// Unique identifier for the entity.
        /// Automatically initialized with a new GUID.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// UTC timestamp when the entity was created.
        /// Stamped automatically by <see cref="AuditDbContext"/> on save, and by host-side
        /// seeders for <c>HasData</c> seed rows.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Identifier of the actor who created the entity.
        /// Stamped automatically by <see cref="AuditDbContext"/> on save, and by host-side
        /// seeders for <c>HasData</c> seed rows.
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when the entity was last modified.
        /// Null if the entity has never been modified.
        /// Stamped automatically by <see cref="AuditDbContext"/> on save.
        /// </summary>
        public DateTime? Modified { get; set; }

        /// <summary>
        /// Identifier of the actor who last modified the entity.
        /// Null if the entity has never been modified.
        /// Stamped automatically by <see cref="AuditDbContext"/> on save.
        /// </summary>
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Status flag indicating whether the record is active or soft-deleted.
        /// <list type="bullet">
        /// <item><description>0 - Active data</description></item>
        /// <item><description>1 - Soft-deleted data</description></item>
        /// </list>
        /// </summary>
        public int RowStatus { get; set; } = 0;
    }
}
