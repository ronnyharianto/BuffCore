# BuffCore.Data

EF Core conventions for .NET services: a base entity with audit fields and soft-delete status, an audit-stamping `DbContext` with attribute-driven schema mapping, provider-neutral registration, and a startup migration helper.

## What's inside

| Type | Purpose |
| --- | --- |
| `EntityBase` | Key (`Guid`), audit fields (`Created`, `CreatedBy`, `Modified`, `ModifiedBy`), and `RowStatus` (0 = active, 1 = soft-deleted). |
| `EntityBaseBuilder<TEntity>` | `IEntityTypeConfiguration<TEntity>` applying the soft-delete query filter (`RowStatus == 0`). |
| `AuditDbContext` | `DbContext` base that stamps audit fields on save from the request-scoped `CurrentUserAccessor` (from BuffCore.Abstractions) and maps entities to schemas via `[DatabaseSchema]` (default `public`). |
| `DatabaseSchemaAttribute` | Declares the database schema for an entity class: `[DatabaseSchema("master_data")]`. |
| `DatabaseConfig` | Connection settings (`ConnectionString`, `CommandTimeout` = 60s default) bound by the host from its own configuration. |
| `AddBuffCoreData<TDbContext>` | Registers the context with the migrations assembly set to the context's assembly and `CommandTimeout` enforced. Provider-neutral: the host picks the engine. |
| `MigrateDatabaseAsync<TDbContext>` | `IHost` extension applying pending migrations at startup (optional `ensureCreated`). |
| `DbCollationHelper`, `DatabaseEngine`, `CollationConstant` | Collation names per engine for case-sensitive/insensitive comparisons (SQL Server, PostgreSQL). |

## Usage

### 1. Entities

```csharp
using BuffCore.Data;

[DatabaseSchema("master_data")]
public class Customer : EntityBase
{
    public string Name { get; set; } = string.Empty;
}
```

### 2. Context

Derive from `AuditDbContext`. Audit fields are stamped automatically on `SaveChanges`/`SaveChangesAsync`
for `Added` and `Modified` entries; `OnModelCreating` maps each `DbSet<T>` to the schema declared on `T`
(or `public` when the attribute is absent).

```csharp
public class AppDbContext(DbContextOptions options, CurrentUserAccessor currentUser)
    : AuditDbContext(options, currentUser)
{
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        new EntityBaseBuilder<Customer>().Configure(modelBuilder.Entity<Customer>()); // soft-delete filter
    }
}
```

### 3. Registration (composition root owns configuration)

The host reads its own configuration and chooses the provider — this package has no dependency on any
specific one. `AddBuffCoreData` applies the generic conventions (migrations assembly, command timeout).

```csharp
var databaseConfig = configuration.GetSection("DatabaseConfig").Get<DatabaseConfig>() ?? new();

services.AddBuffCoreData<AppDbContext>(databaseConfig, x =>
    x.UseNpgsql(databaseConfig.ConnectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
```

### 4. Startup migration

```csharp
var app = builder.Build();
await app.MigrateDatabaseAsync<AppDbContext>();
```

## Design decisions

- **Audit actor via `CurrentUserAccessor`.** The context takes the request-scoped accessor (from BuffCore.Abstractions) that the host populates from authentication middleware, mirroring how BuffCore.Utilities helpers receive their collaborators. No accessor means audit stamps are written with an empty actor — suitable for tests and background jobs.
- **Logger, not static Serilog.** Following the decision recorded for `JsonHelper`, the context logs through an optional `ILogger` resolved by the host, so the package stays free of logging-stack opinions.
- **Provider neutrality.** `AddBuffCoreData` deliberately takes a `configureProvider` delegate instead of referencing Npgsql/SQL Server SDKs. Engine choice is the host's; the `DbCollationHelper` constants remain descriptive data rather than a provider dependency.
- **Migrations assembly = the context's assembly.** Deterministic (`typeof(TDbContext).Assembly`) rather than the registration call site, keeping `dotnet ef` behavior stable regardless of where registration happens.
- **`EntityBase` setters are public.** Host-side seeders (`HasData`) must stamp `Created`/`CreatedBy` at model-building time; the audit context overwrites them on save. Treat them as infrastructure-managed.
- **`EnsureCreated` is opt-in and off by default.** With migrations in place it is redundant; hosts preserving legacy behavior may enable it, but new integrations should rely on `Migrate` alone.

## Validation

The repo's test suite covers this package with SQLite in-memory (audit stamping, soft-delete filtering, schema mapping, registration conventions, startup migration).
