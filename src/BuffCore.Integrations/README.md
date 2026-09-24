# BuffCore.Integrations

Injectable cloud-integration helpers for .NET services: Google Cloud Storage and Firebase Cloud Messaging, with host-owned configuration and DI registration.

## Contents

- **GoogleCloudStorageHelper** — injectable GCS operations against a configured bucket:
  - `UploadFileAsync(stream, contentType, path)` — upload with a generated object name (publicly readable)
  - `UploadFileAsync(stream, fileId, contentType, path)` — upload under a specific name, skipping when the object already exists
  - `RetrieveSignedUrlFileAsync(objectName, fileName, durationInSeconds)` — V4 signed URL with a `response-content-disposition` attachment header
  - `DownloadFileAsync(objectName)` — returns the shared `FileDto` (stream, name, content type)
- **FirebaseMessagingHelper** — injectable FCM sends: `SendToTokenAsync`, `SendMulticastAsync` (returns the SDK `BatchResponse`), `SendDataOnlyToTokenAsync`
- **StorageConfig / GoogleCloudStorage** (`BuffCore.Integrations.Configurations`) — project id, bucket name, service account
- **MessagingConfig / FirebaseMessaging** (`BuffCore.Integrations.Configurations`) — project id, service account
- **GoogleServiceAccount** (`BuffCore.Integrations.Configurations`) — the parsed service-account JSON shape (`type`, `private_key`, `client_email`), `[JsonProperty]`-annotated to serialize exactly as the Google SDK expects
- **`AddBuffCoreIntegrations(storageConfig, messagingConfig)`** — singleton registration; pass `null` for an integration that the host does not use

## Usage

```csharp
using BuffCore.Integrations;
using BuffCore.Integrations.Configurations;

// In Program.cs — the host owns where the credentials come from.
var storage = builder.Configuration.GetSection("StorageConfig").Get<StorageConfig>();
var messaging = builder.Configuration.GetSection("MessagingConfig").Get<MessagingConfig>();
builder.Services.AddBuffCoreIntegrations(storage, messaging);

// Injected
public class AttachmentService(GoogleCloudStorageHelper storage)
{
    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct)
        => await storage.UploadFileAsync(content, contentType, "attachments", ct);
}

public class Notifier(FirebaseMessagingHelper fcm)
{
    public Task<BatchResponse> AnnounceAsync(IEnumerable<string> tokens, CancellationToken ct)
        => fcm.SendMulticastAsync(tokens, "Hello", "Body", data: null, ct);
}
```

Hosts may also construct helpers directly with `new GoogleCloudStorageHelper(storageConfig)` — no DI required.

> **The section names are host conventions, not library contracts.** BuffCore only ever sees the bound instances.

## Design decisions

- **Instance helpers over static classes.** Matches the BuffCore.Utilities canon (JsonHelper, EmailHelper, RsaHelper): injectable, mockable, `ILogger`-based, no hidden global state.
- **Streams, not `IFormFile`.** Keeps the package usable outside ASP.NET Core; web hosts pass `file.OpenReadStream()`.
- **Host-owned configuration.** Same contract as Utilities: the composition root sources credentials (configuration, user secrets, key vault, code); the library never reads `IConfiguration` and never logs credential material.
- **Fail fast at construction.** A missing or incomplete service account throws `InvalidOperationException` when the helper is built, instead of surfacing as a null-dereference on first use.
- **Named Firebase apps.** Each `FirebaseMessagingHelper` owns a uniquely named `FirebaseApp` rather than the global default, so multiple instances (hosts, tests) cannot collide on FirebaseAdmin's static registry.
- **Publicly readable uploads preserved.** The GCS helper keeps the original host behavior (`PredefinedObjectAcl.PublicRead`); hosts needing private objects should adjust the options when adopting the package.
- **Send operations are integration-tested.** FCM resolves through the real SDK; unit tests cover construction, configuration validation, and DI semantics, while live sends are verified by the consuming product.

## Dependencies

`Google.Cloud.Storage.V1`, `FirebaseAdmin` (and their transitive `Google.Apis.*` stack), plus `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`. BuffCore.Abstractions is referenced for the shared `FileDto`.
