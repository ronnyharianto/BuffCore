namespace BuffCore.Utilities.Configurations
{
    /// <summary>
    /// SMTP server settings for <see cref="EmailHelper"/>.
    /// </summary>
    public class SmtpConfig
    {
        /// <summary>
        /// The SMTP server host (e.g. "smtp.gmail.com").
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// The SMTP server port. Common values: 587 (STARTTLS), 465 (implicit SSL).
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Whether the client uses SSL/TLS.
        /// </summary>
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// The user name used to authenticate with the SMTP server.
        /// Usually the sender email address.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// The password used to authenticate with the SMTP server.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
