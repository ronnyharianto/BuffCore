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
- **HashHelper** — `ComputeSha256` for deterministic text hashing (API key hashes, checksums); hex or Base64 output
- **RsaHelper** — injectable RSA encrypt/decrypt from PEM keys (OAEP-SHA512, Base64 ciphertext); reconfigurable for key rotation, lenient failures
- **RsaConfig** (`BuffCore.Utilities.Configurations`) — PEM public/private key pair for `RsaHelper`
- **EmailHelper** — injectable SMTP sender with configurable host/port/SSL and per-call credentials; returns `false` on failure instead of throwing
- **EmailMessage / EmailAddress** (`BuffCore.Utilities.Objects`) — credentials-free email model; the password lives only in `SmtpConfig`
- **SmtpConfig** (`BuffCore.Utilities.Configurations`) — SMTP server settings (host, port, SSL, user name, password)
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

### Email usage

```csharp
// In Program.cs — registered by AddBuffCoreUtilities(); configure default SMTP host-side:
var smtp = builder.Configuration.GetSection("SmtpConfig").Get<SmtpConfig>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<EmailHelper>().Configure(smtp!));

// Injected
public class NotificationSender(EmailHelper email)
{
    public Task<bool> SendWelcomeAsync(CancellationToken ct)
        => email.SendAsync(new EmailMessage
        {
            From = "noreply@example.com",
            FromDisplayName = "Example App",
            Subject = "Welcome",
            Body = "<p>Hello!</p>",
            To = new EmailAddress("jane@example.com", "Jane Doe"),
        }, ct);
}
```

`EmailMessage` carries no credentials — server settings and the password are supplied via `SmtpConfig` (per call or via `Configure`). Send failures are logged and return `false`; the caller decides what a failed notification means.

### Hashing and RSA usage

```csharp
HashHelper.ComputeSha256(apiKey);                      // uppercase hex — e.g. ApiClient.ApiKeyHash
HashHelper.ComputeSha256(payload, "base64");           // Base64 output when needed

await rsa.Initialize(new RsaConfig { PublicKey = pemPublic, PrivateKey = pemPrivate });
var cipher = rsa.Encrypt("secret");                    // Base64, empty on failure (logged)
var plain = rsa.Decrypt(cipher);
```

`RsaHelper` is stateless-per-operation (keys are imported into a fresh `RSA` per call) and lenient: missing initialization or a failed operation logs and returns `string.Empty`.

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
- **Credentials stay out of message objects.** `EmailMessage` describes only the letter; `SmtpConfig` carries the server settings and password. This mirrors the JSON/HTTP contract above — the composition root owns secrets — and avoids the earlier pattern where a shared email DTO carried the account password through every layer.
- **Lenient failure contract for sends.** `EmailHelper` and `RsaHelper` log failures and return a sentinel (`false` / `string.Empty`) rather than throwing, matching `JsonHelper`'s lenient deserialization contract. Callers who need hard failures can check the return value.

## Dependencies

BCL plus `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, and `Microsoft.Extensions.Http` (for the typed-client registration). No serialization package — System.Text.Json ships with the runtime; SMTP and RSA support ship with the BCL (`System.Net.Mail`, `System.Security.Cryptography`).
