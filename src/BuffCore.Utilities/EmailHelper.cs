using BuffCore.Utilities.Configurations;
using BuffCore.Utilities.Objects;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace BuffCore.Utilities
{
    /// <summary>
    /// Sends email over configurable SMTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Server settings come from <see cref="SmtpConfig"/> passed per call or via
    /// <see cref="Configure"/>, replacing any hardcoded provider defaults. Body is sent as HTML.
    /// </para>
    /// <para>
    /// Sends are lenient: failures are logged (when a logger is available) and the method
    /// returns <c>false</c> instead of throwing.
    /// </para>
    /// </remarks>
    public class EmailHelper(ILogger? logger = null, Func<SmtpConfig, SmtpClient>? smtpClientFactory = null)
    {
        private readonly ILogger? _logger = logger;
        private readonly Func<SmtpConfig, SmtpClient> _smtpClientFactory = smtpClientFactory ?? CreateSmtpClient;
        private SmtpConfig? _config;

        /// <summary>
        /// Configures (or reconfigures) the default SMTP settings used when
        /// <see cref="SendAsync(EmailMessage, CancellationToken)"/> is called without explicit settings.
        /// </summary>
        /// <param name="config">The SMTP server settings.</param>
        /// <returns>The helper instance for fluent chaining.</returns>
        public EmailHelper Configure(SmtpConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        /// <summary>
        /// Sends an email using the previously configured SMTP settings.
        /// </summary>
        /// <param name="message">The message to send (credentials-free).</param>
        /// <param name="cancellationToken">Propagates notification that the send should be canceled.</param>
        /// <returns><c>true</c> when the message is handed to the SMTP server; otherwise <c>false</c>.</returns>
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
            => SendAsync(message, _config, cancellationToken);

        /// <summary>
        /// Sends an email using the supplied SMTP settings.
        /// </summary>
        /// <param name="message">The message to send (credentials-free).</param>
        /// <param name="smtpConfig">The SMTP server settings (host, port, SSL, credentials).</param>
        /// <param name="cancellationToken">Propagates notification that the send should be canceled.</param>
        /// <returns><c>true</c> when the message is handed to the SMTP server; otherwise <c>false</c>.</returns>
        public async Task<bool> SendAsync(EmailMessage message, SmtpConfig? smtpConfig, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            var config = smtpConfig ?? _config;
            if (config is null)
            {
                _logger?.LogError("Email send failed: no SMTP configuration was provided.");
                return false;
            }

            try
            {
                using var smtp = _smtpClientFactory(config);

                using var mail = ToMailMessage(message);
                await smtp.SendMailAsync(mail, cancellationToken);

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Email sent to {Recipients} via {Host}:{Port}.", CountRecipients(message), config.Host, config.Port);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Email send failed: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Builds the default network SMTP client from the supplied settings.
        /// </summary>
        private static SmtpClient CreateSmtpClient(SmtpConfig config)
            => new()
            {
                Host = config.Host,
                Port = config.Port,
                EnableSsl = config.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(config.UserName, config.Password),
            };

        /// <summary>
        /// Maps the credentials-free message to an SMTP mail message.
        /// </summary>
        private static MailMessage ToMailMessage(EmailMessage message)
        {
            var from = new MailAddress(message.From, message.FromDisplayName);
            var mail = new MailMessage
            {
                From = from,
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = true,
            };

            if (message.MultiRecipients.Count > 0)
            {
                foreach (var recipient in message.MultiRecipients)
                {
                    mail.To.Add(new MailAddress(recipient.Email, recipient.DisplayName));
                }
            }
            else if (message.To is not null)
            {
                mail.To.Add(new MailAddress(message.To.Email, message.To.DisplayName));
            }

            return mail;
        }

        /// <summary>
        /// Counts recipients for log output without exposing addresses.
        /// </summary>
        private static int CountRecipients(EmailMessage message)
            => message.MultiRecipients.Count > 0 ? message.MultiRecipients.Count : (message.To is null ? 0 : 1);
    }
}
