using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace PharmaCare.Services
{
    public class EmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            var fromEmail = _config["Smtp:From"];
            message.From.Add(MailboxAddress.Parse(fromEmail));

            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = htmlBody
            };

            using (var smtp = new SmtpClient())
            {
                var host = _config["Smtp:Host"];
                var port = int.TryParse(_config["Smtp:Port"], out int safePort) ? safePort : 587;
                var user = _config["Smtp:User"];
                var pass = _config["Smtp:Pass"];

                await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(user, pass);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);

                Console.WriteLine($"✅ Email sent successfully to {to}");
            }
        }
    }
}
