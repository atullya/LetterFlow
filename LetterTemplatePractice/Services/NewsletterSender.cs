using LetterTemplatePractice.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LetterTemplatePractice.Services
{
    /// <summary>
    /// Sends the daily news digest to a list of subscribers via SMTP (MailKit).
    /// Configure SMTP credentials in appsettings.json under "Newsletter:Smtp".
    /// </summary>
    public sealed class NewsletterSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NewsletterSender> _logger;

        public NewsletterSender(IConfiguration config, ILogger<NewsletterSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Sends the digest HTML to every subscriber.
        /// Returns the number of emails successfully sent.
        /// </summary>
        public async Task<int> SendDigestAsync(
            IReadOnlyList<NewsletterSubscription> subscribers,
            IReadOnlyList<NewsStory> stories,
            string baseUrl,
            CancellationToken ct = default)
        {
            if (subscribers.Count == 0 || stories.Count == 0)
                return 0;

            var smtpSection = _config.GetSection("Newsletter:Smtp");
            var host        = smtpSection["Host"]     ?? throw new InvalidOperationException("Newsletter:Smtp:Host is not configured.");
            var port        = smtpSection.GetValue<int>("Port", 587);
            var username    = smtpSection["Username"] ?? throw new InvalidOperationException("Newsletter:Smtp:Username is not configured.");
            var password    = smtpSection["Password"] ?? throw new InvalidOperationException("Newsletter:Smtp:Password is not configured.");
            var fromName    = smtpSection["FromName"]    ?? "LetterFlow Stories";
            var fromAddress = smtpSection["FromAddress"] ?? username;
            var subject     = smtpSection["Subject"]     ?? $"Your Daily Digest — {DateTime.UtcNow:MMMM d, yyyy}";

            int sent = 0;

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(username, password, ct);

            foreach (var sub in subscribers)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var unsubUrl = $"{baseUrl}/Newsletter/Unsubscribe?token={sub.UnsubscribeToken}";
                    var html     = NewsletterEmailBuilder.Build(stories, unsubUrl, DateTime.UtcNow);

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(fromName, fromAddress));
                    message.To.Add(new MailboxAddress(sub.User?.DisplayName ?? sub.Email, sub.Email));
                    message.Subject = subject;
                    message.Body    = new TextPart("html") { Text = html };

                    await smtp.SendAsync(message, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NewsletterSender: Failed to send to {Email}", sub.Email);
                }
            }

            await smtp.DisconnectAsync(true, ct);
            return sent;
        }
    }
}
