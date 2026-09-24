using BuffCore.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BuffCore.Web.Server
{
    /// <summary>
    /// Constants for the well-known claim names understood by <see cref="CurrentClaimsMapper"/>.
    /// </summary>
    /// <remarks>
    /// Defaults match the Hireme.Expensia convention ("current_company", "permissions"); hosts with
    /// different claim shapes can pass their own names to
    /// <see cref="CurrentClaimsMapper.Map(ClaimsPrincipal, CurrentClaimsOptions?)"/> instead.
    /// </remarks>
    public static class BuffCoreClaimTypes
    {
        /// <summary>
        /// The claim that stores the Id of the company the user is currently accessing.
        /// This value may change if the user switches companies within the application.
        /// </summary>
        public const string CurrentCompany = "current_company";

        /// <summary>
        /// The claim that stores a list of permission codes associated with the authenticated user.
        /// This is typically used for authorization checks throughout the system.
        /// </summary>
        public const string Permissions = "permissions";
    }

    /// <summary>
    /// Overrides for the claim-type names read by <see cref="CurrentClaimsMapper"/>.
    /// Unset members fall back to the built-in defaults.
    /// </summary>
    public class CurrentClaimsOptions
    {
        /// <summary>
        /// Claim type carrying the user's unique identifier. Defaults to the JWT <c>sid</c> claim.
        /// </summary>
        public string? UserIdClaimType { get; set; }

        /// <summary>
        /// Claim type carrying the user's full name. Defaults to <see cref="ClaimTypes.GivenName"/>.
        /// </summary>
        public string? FullNameClaimType { get; set; }

        /// <summary>
        /// Claim type carrying the user's email address. Defaults to <see cref="ClaimTypes.Email"/>.
        /// </summary>
        public string? EmailAddressClaimType { get; set; }

        /// <summary>
        /// Claim type carrying the current company Id. Defaults to <see cref="BuffCoreClaimTypes.CurrentCompany"/>.
        /// </summary>
        public string? CompanyIdClaimType { get; set; }

        /// <summary>
        /// Claim type carrying permission codes (possibly repeated). Defaults to <see cref="BuffCoreClaimTypes.Permissions"/>.
        /// </summary>
        public string? PermissionsClaimType { get; set; }
    }

    /// <summary>
    /// Maps a <see cref="ClaimsPrincipal"/>'s claims onto a <see cref="CurrentUserAccessor"/>,
    /// centralizing the identity extraction normally performed inside an authorization filter.
    /// </summary>
    public static class CurrentClaimsMapper
    {
        /// <summary>
        /// Maps the claims of the given principal onto a new <see cref="CurrentUserAccessor"/>.
        /// </summary>
        /// <param name="principal">The authenticated user's principal.</param>
        /// <param name="options">
        /// Optional claim-name overrides; when null, the built-in defaults are used.
        /// </param>
        /// <returns>The populated <see cref="CurrentUserAccessor"/>.</returns>
        public static CurrentUserAccessor Map(ClaimsPrincipal principal, CurrentClaimsOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(principal);

            options ??= new CurrentClaimsOptions();

            var identity = principal.Identity as ClaimsIdentity;
            var claims = identity?.Claims ?? [];

            var accessor = new CurrentUserAccessor
            {
                UserId = ParseGuid(claims.FirstOrDefault(c => c.Type == (options.UserIdClaimType ?? JwtRegisteredClaimNames.Sid))?.Value),
                FullName = claims.FirstOrDefault(c => c.Type == (options.FullNameClaimType ?? ClaimTypes.GivenName))?.Value ?? string.Empty,
                EmailAddress = claims.FirstOrDefault(c => c.Type == (options.EmailAddressClaimType ?? ClaimTypes.Email))?.Value ?? string.Empty,
                CompanyId = ParseGuid(claims.FirstOrDefault(c => c.Type == (options.CompanyIdClaimType ?? BuffCoreClaimTypes.CurrentCompany))?.Value),
                Permissions = claims.Where(c => c.Type == (options.PermissionsClaimType ?? BuffCoreClaimTypes.Permissions)).Select(c => c.Value)
            };

            return accessor;
        }

        /// <summary>
        /// Parses a claim value into a <see cref="Guid"/>, mapping null/empty/invalid values to <see cref="Guid.Empty"/> —
        /// matching the historical behavior of the authorization filters this mapper was extracted from.
        /// </summary>
        private static Guid ParseGuid(string? value)
            => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
    }
}
