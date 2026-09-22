# BuffCore.Utilities

General-purpose utilities for .NET services. Pure BCL building blocks with no framework or serialization dependencies — safe to reference from any layer.

## Contents

- **EnumHelper** — extension and static helpers over enums:
  - `GetDescription()` — the `[Description]` text of an enum value, falling back to the member name
  - `FilterEnumList<TEnum>(filterKey)` — case-insensitive substring filter over enum descriptions
  - `RetrieveEnumList<TEnum>()` — every value with its `[EnumMember]` value and description, as `EnumDto`
  - `RetrieveEnumList<TEnum, TMapFromEnum>()` — same, plus enum-to-enum mapping via `MapFromAttribute<T>`
- **MapFromAttribute\<T\>** — attribute that maps an enum member to a value of another enum type
- **DateRangeHelper** — `CountDaysBetween` inclusive day counting with per-weekday exclusions
- **JsonHelper** — injectable STJ serializer (PascalCase output, case-insensitive read, null-omitting, cycle-safe); deserialization failures are logged and return `default` instead of throwing
- **HttpClientHelper** — injectable typed HTTP client for JSON POST/GET and form-URL-encoded POST flows, with optional Basic/Bearer auth
- **HttpClientConfig** (`BuffCore.Utilities.Configurations`) — timeout and handler-lifetime settings for the typed client
- **`AddBuffCoreUtilities()`** — DI registration for the above

## Usage

```csharp
using BuffCore.Utilities;

enum OrderStatus
{
    [Description("Awaiting payment")]
    Pending,

    [Description("Shipped")]
    Shipped,
}

"Shipped".GetDescription();               // "Shipped"
EnumHelper.FilterEnumList<OrderStatus>("ship");        // [OrderStatus.Shipped]
EnumHelper.RetrieveEnumList<OrderStatus>();            // List<EnumDto>
DateRangeHelper.CountDaysBetween(start, end, [DayOfWeek.Saturday, DayOfWeek.Sunday]);
```

`EnumDto` comes from `BuffCore.Abstractions`, which this package references.

## HTTP and JSON usage

**Configuration contract.** The host's composition root owns configuration sourcing: this package never reads `appsettings.json`, environment variables, or `IConfiguration`. It defines the options shape (`HttpClientConfig`) and applies the instance the host passes in — keeping the package source-agnostic (env vars, user secrets, Key Vault, command line all work) and free of `Microsoft.Extensions.Configuration.*` dependencies.

```csharp
using BuffCore.Utilities;

// In Program.cs
builder.Services.AddBuffCoreUtilities(builder.Configuration
    .GetSection("HttpClientConfig").Get<HttpClientConfig>());

// Injected
public class MyClient(HttpClientHelper http)
{
    public Task<TokenResponse?> GetTokenAsync(CancellationToken ct)
        => http.PostAsync<TokenRequest, TokenResponse>(
            "https://auth.example.com/token",
            new TokenRequest { ClientId = "..." },
            Convert.ToBase64String("id:secret"u8),
            ct);
}
```

`JsonHelper` serializes with property names as declared (PascalCase) and reads case-insensitively; it omits nulls, handles cycles, and logs deserialization failures at Warning when a logger is available.

### Configuring `HttpClientConfig`

| Setting | Default | Meaning |
| --- | --- | --- |
| `Timeout` | 60 seconds | Request timeout for the typed client. |
| `HandlerLifetime` | 5 minutes | How long pooled `HttpMessageHandler` instances are reused. |

Supported host patterns:

```csharp
// 1. From configuration — the host owns the section name and the binding:
services.AddBuffCoreUtilities(builder.Configuration
    .GetSection("HttpClientConfig").Get<HttpClientConfig>());

// 2. From code, or nothing at all for the defaults above:
services.AddBuffCoreUtilities();                                       // defaults
services.AddBuffCoreUtilities(new HttpClientConfig { Timeout = 10 });  // explicit values
```

Hosts whose conventions prefer the Options pattern compose it host-side — bind the section (e.g. `services.Configure<HttpClientConfig>(...)`), then pass the resulting instance to `AddBuffCoreUtilities`. The library never binds `IOptions` itself, so it stays usable in hosts without a configuration system at all.

> **The section name is a host convention, not a library contract.** `HttpClientConfig` happens to be the section name Hireme.Expensia uses; a host may name the section anything or source the values elsewhere entirely — the library only ever sees the bound instance.

## Design decisions

- **Parameter-based configuration over library-read configuration.** The composition root decides where settings come from (JSON, environment variables, Key Vault, code). This keeps the package source-agnostic and free of `Microsoft.Extensions.Configuration.*` dependencies, and keeps the configuration contract visible in host code instead of hidden inside a JSON file the library would have to locate itself.
- **Instance `JsonHelper` rather than a static class.** The optional `ILogger` is instance state, which enables the lenient log-and-return-`default` contract and lets consumers mock the helper. `JsonSerializerOptions` are still cached in static fields and the DI registration is a singleton, so there is no per-use cost relative to a static API.
- **When to revisit.** If a second configurable helper lands in this package, or BuffCore.Data / BuffCore.Web.Server standardize on the Options pattern, evaluate `IOptions<HttpClientConfig>` for the registration while keeping the parameter overload through a deprecation window. Not planned at 0.1.0.

## Dependencies

BCL plus `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, and `Microsoft.Extensions.Http` (for the typed-client registration). No serialization package — System.Text.Json ships with the runtime.
