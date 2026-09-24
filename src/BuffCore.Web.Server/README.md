# BuffCore.Web.Server

ASP.NET Core server conventions for .NET APIs: a versioned `BaseController`, a commit/rollback `TransactionFilter<TDbContext>`, JWT-claims to `CurrentUserAccessor` mapping, and `WebApplicationBuilder` extensions for controllers, Swagger, CORS, and JWT bearer authentication — so every service boots with the same stack without re-scaffolding `Program.cs`.

## Installation

Add the package (or project) reference together with **BuffCore.Abstractions**:

```xml
<PackageReference Include="BuffCore.Web.Server" Version="0.1.0" />
```

## BaseController

Derive from `BaseController` to inherit the `api/v1/[controller]` route convention and ApiController behaviors:

```csharp
using BuffCore.Web.Server;

public class CustomerController(ICustomerService _customerService) : BaseController
{
    [HttpGet("{id}")]
    public async Task<ObjectDto<CustomerDto>> Get(Guid id, CancellationToken ct)
        => await _customerService.GetAsync(id, ct);
}
```

## TransactionFilter&lt;TDbContext&gt;

Wraps each API action in a database transaction. Mutations (`POST`/`PUT`/`PATCH`/`DELETE`) commit when the action returns a `BaseDto` with a 2xx code (or when `CommitTransaction` is forced); everything else rolls back. Exceptions are converted into a 500 `BaseDto` with the trace Id:

```csharp
builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<TransactionFilter<ApplicationDbContext>>();
    });
```

The action's `ObjectResult` value must be a `BaseDto` derivative — the filter stamps `baseDto.Id` with `HttpContext.TraceIdentifier` and mirrors `baseDto.Code` onto the response status code.

## CurrentClaimsMapper

Maps an authenticated `ClaimsPrincipal` onto `CurrentUserAccessor` (UserId, FullName, EmailAddress, CompanyId, Permissions) using the well-known claim names in `BuffCoreClaimTypes` (`current_company`, `permissions`). Override any claim name via `CurrentClaimsOptions`:

```csharp
var accessor = CurrentClaimsMapper.Map(httpContext.User);
// or with custom claim names:
var accessor = CurrentClaimsMapper.Map(httpContext.User, new CurrentClaimsOptions
{
    CompanyIdClaimType = "tenant_id",
});
```

## Startup extensions

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBuffCoreControllers();
builder.AddBuffCoreSwaggerGen("My.Service.API");
builder.AddBuffCoreCors(corsOrigins);
builder.AddBuffCoreJwtBearer(new JwtBearerConfig
{
    Issuer = "https://localhost:5000",
    Audience = "My Service User",
    SecretKey = configuration["AuthenticationConfig:JwtOption:SecretKey"]!,
    ClockSkew = TimeSpan.Zero,
});

var app = builder.Build();

app.UseAuthentication().UseAuthorization();
app.MapControllers();
app.Run();
```

`AddBuffCoreControllers` enables lowercase routes/query strings and Newtonsoft.Json formatters (`ReferenceLoopHandling.Ignore`, `NullValueHandling.Ignore`); `AddBuffCoreSwaggerGen` registers the Bearer security scheme and annotations support.
