namespace BuffCore.Utilities.Configurations
{
    /// <summary>
    /// Represents the configuration for RSA cryptographic keys in PEM format.
    /// </summary>
    /// <remarks>
    /// Keys are expected in PEM encoding (e.g. "-----BEGIN PUBLIC KEY-----").
    /// Secrets belong in a secret store (user secrets, environment variables, or a vault),
    /// not in committed configuration files.
    /// </remarks>
    public class RsaConfig
    {
        /// <summary>
        /// The RSA public key in PEM format.
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// The RSA private key in PEM format.
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty;
    }
}
