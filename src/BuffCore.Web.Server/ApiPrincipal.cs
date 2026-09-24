namespace BuffCore.Web.Server
{
    /// <summary>
    /// Host-agnostic view of an API client used by <see cref="PermissionAuthorizationFilter"/> for
    /// API-key authentication. Hosts materialize this from their own client storage via the
    /// <c>authorizeApiClient</c> delegate passed to <c>AddPermissionAuthorizationFilter</c>.
    /// </summary>
    public class ApiPrincipal
    {
        /// <summary>
        /// Identifier copied into <c>CurrentUserAccessor.UserId</c> on successful
        /// API-key authentication.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Public client identifier supplied in the <c>X-CLIENT-ID</c> header.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable client name copied into <c>CurrentUserAccessor.FullName</c>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// SHA-256 hash of the API key; compared ordinally against the hashed <c>X-API-KEY</c> header.
        /// </summary>
        public string ApiKeyHash { get; set; } = string.Empty;

        /// <summary>
        /// Expiration timestamp of the API key, or null when the key does not expire.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether the client is enabled. Disabled clients never authenticate.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
