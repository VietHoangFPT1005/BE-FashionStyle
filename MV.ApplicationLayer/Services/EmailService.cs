using Microsoft.Extensions.Options;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using System.Net;
using System.Net.Mail;

namespace MV.ApplicationLayer.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IOptions<SmtpSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var fromAddress = new MailAddress(_smtpSettings.SenderEmail!, _smtpSettings.SenderName);
            var toAddress = new MailAddress(to);

            using var smtp = new SmtpClient
            {
                Host = _smtpSettings.Server!,
                Port = _smtpSettings.Port,
                EnableSsl = _smtpSettings.EnableSsl,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
        }
    }
}
