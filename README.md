# BuffCore

BuffCore is a set of small, reusable .NET foundation libraries for building HTTP APIs. It provides standard response envelopes, paging and search request contracts, and shared primitives — so every service speaks the same wire format without re-scaffolding the basics.

## Packages

| Package | Status | Contents |
| --- | --- | --- |
| **BuffCore.Abstractions** | Available (0.1.0) | Response envelopes (`BaseDto`, `ObjectDto<T>`, `StructDto<T>`, `PagingDto<T>`), supporting DTOs, paging/search input bases, `CurrentUserAccessor`. |
| **BuffCore.Utilities** | Available (0.1.0) | Enum description/mapping helpers (`EnumHelper`, `MapFromAttribute<T>`), date-range calculations (`DateRangeHelper`), injectable JSON + HTTP helpers (`JsonHelper`, `HttpClientHelper`, `AddBuffCoreUtilities`). |

## Planned packages

The library family grows in small, independently shippable steps:

1. ✅ **Abstractions** — response/input contracts
2. ✅ **Utilities** — general-purpose BCL helpers (enums, dates, and more over time)
3. **BuffCore.Data** — base entity, entity builder, and DbContext conventions for EF Core (also absorbs `PagingDto.ApplyPagination` so Abstractions can drop its EF dependency)
3. **BuffCore.Web.Server** — base controller, transaction filter, and JWT/Swagger/CORS setup extensions for ASP.NET Core
4. **BuffCore.Web.UI** — Razor Class Library with shared JavaScript helpers

## Usage

Add the package reference and use the standard envelopes:

```csharp
using BuffCore.Abstractions.Dtos;

[HttpGet("paging")]
public async Task<PagingDto<ProjectDto>> Paging(PagingSearchInputBase input, CancellationToken ct)
{
    var paging = new PagingDto<ProjectDto>();
    await paging.ApplyPagination(input.Page, input.PageSize, _query, ct);
    return paging;
}
```

Serialization is annotated for **System.Text.Json**; `BaseDto.CommitTransaction` is `[JsonIgnore]` — a server-side transaction-control flag, not part of the API contract. Newtonsoft hosts should exclude it via a custom `DefaultContractResolver` (see package README).

## Development

```powershell
dotnet build BuffCore.sln
dotnet test BuffCore.sln
```

Publishing (after feed setup): CI packs on every build; the publish job activates once the feed is configured (`vars.NUGET_PUBLISH_ENABLED == 'true'`).

## Contributing

Issues and pull requests are welcome. Keep new APIs small, dependency-free where possible, and covered by tests.
