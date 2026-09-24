namespace BuffCore.Integrations.Configurations
{
    /// <summary>
    /// Google service-account credentials, carried as the parsed JSON file shape
    /// (<c>type</c>, <c>private_key</c>, <c>client_email</c>). The host owns where the
    /// credentials come from — configuration, user secrets, a key vault, or code — this
    /// type only describes them.
    /// </summary>
    public class GoogleServiceAccount
    {
        /// <summary>
        /// The type of service account (typically <c>service_account</c>).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The private key associated with the service account.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("private_key")]
        public string PrivateKey { get; set; } = string.Empty;

        /// <summary>
        /// The client email associated with the service account.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("client_email")]
        public string ClientEmail { get; set; } = string.Empty;
    }
}
