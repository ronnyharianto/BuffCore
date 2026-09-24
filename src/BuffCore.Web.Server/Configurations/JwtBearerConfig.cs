namespace BuffCore.Web.Server.Configurations
{
    /// <summary>
    /// Typed settings for JWT bearer authentication, mapped by the host from its own
    /// configuration (e.g. an <c>AuthenticationConfig:JwtOption</c> section) and passed to
    /// <c>AddBuffCoreJwtBearer</c>.
    /// </summary>
    public class JwtBearerConfig
    {
        /// <summary>
        /// The token issuer (e.g., your domain or service name).
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// The intended recipient (audience) of the token.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Secret key used to sign the JWT token.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Allowed clock skew when validating token lifetimes. Defaults to
        /// <see cref="TimeSpan.Zero"/> so expired tokens are rejected immediately.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.Zero;
    }
}
