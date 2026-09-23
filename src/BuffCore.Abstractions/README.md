# BuffCore.Abstractions

Standard API response and request contracts for .NET services. Reference this package to get consistent response envelopes, paging/search input bases, and a request-scoped current-user accessor — so every service speaks the same wire format from day one.

## Contents

| Type | Purpose |
| --- | --- |
| `BaseDto` | Base response envelope: `Code`, `Succeeded`, `Message`, `Id` (trace id), `CommitTransaction` (internal pipeline flag, excluded from JSON). |
| `ObjectDto<T>` | Response carrying a reference-type payload in `Obj`. |
| `StructDto<T>` | Response carrying a value-type payload in `Obj`. |
| `PagingDto<T>` | Paginated response with `Page`, `PageSize`, `TotalPage`, `RecordsFiltered`, `HasNext`, `HasPrevious`, plus `ApplyPagination` over an `IQueryable<T>` and `CopyPagination`. |
| `EnumDto` | Enum entry DTO (value, mapped value, member name, description). |
| `FileDto` | File descriptor DTO (stream, path, name, size, content type). |
| `UploadDto` | Upload result DTO (object name and URL). |
| `TimeZoneDto` | Time zone identifier/display-name pair. |
| `CurrentUserAccessor` | Request-scoped identity: `UserId`, `FullName`, `EmailAddress`, `CompanyId`, `Permissions`. |
| `IPagingInput` / `PagingInputBase` | Page/PageSize request contract with `[Range]` validation. |
| `ISearchInput` / `SearchInputBase` | `SearchKey` request contract. |
| `PagingSearchInputBase` | Paging + search combined input base. |

## Quick start

```csharp
using BuffCore.Abstractions.Dtos;

public async Task<PagingDto<CustomerDto>> Search(PagingSearchInputBase input, CancellationToken ct)
{
    var query = _dbContext.Customers.AsNoTracking().Select(c => new CustomerDto { ... });
    var paging = new PagingDto<CustomerDto>();
    await paging.ApplyPagination(input.Page, input.PageSize, query, ct);
    return paging;
}
```

## Serialization

`BaseDto.CommitTransaction` is marked `[JsonIgnore]` (Newtonsoft.Json) — it is a server-side transaction-control flag and never part of the API contract. BuffCore standardizes on Json.NET for all JSON serialization.

## Build and test

```powershell
dotnet build BuffCore.sln
dotnet test BuffCore.sln
```
