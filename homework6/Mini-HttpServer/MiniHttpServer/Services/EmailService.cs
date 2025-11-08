using MiniHttpServer.Settings.EmailSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace MiniHttpServer.Services
{
    internal class EmailService
    {
        private readonly List<SmtpSettings> _smtpList;

        public EmailService()
        {
            _smtpList = new List<SmtpSettings>
            {
                new SmtpSettings
                {
                    Name = "Gmail",
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    Username = "kaanemmedik@gmail.com",
                    Password = "mdae fdvk trgh vsld"
                },
                new SmtpSettings
                {
                    Name = "Mail.ru",
                    Host = "smtp.mail.ru",
                    Port = 587,
                    EnableSsl = true,
                    Username = "kaanemmedik@mail.ru",
                    Password = "nklnjknbJHKB83Nmd"
                }
            };
        }
        public async Task SendEmailAsync(string to, string title, string password)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Alıcı adresi boş olamaz.", nameof(to));

            Exception? lastError = null;

            foreach (var smtpSettings in _smtpList)
            {
                try
                {
                    using var message = BuildMessage(to, title, password);
                    using var smtp = new SmtpClient(smtpSettings.Host, smtpSettings.Port)
                    {
                        EnableSsl = smtpSettings.EnableSsl,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password),
                        Timeout = 100_000
                    };

                    await smtp.SendMailAsync(message).ConfigureAwait(false);
                    Console.WriteLine($"Письмо отправлено через {smtpSettings.Name}");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] {smtpSettings.Name} ile gönderim başarısız: {ex.Message}");
                    lastError = ex;
                }
            }
            throw new InvalidOperationException("E-posta gönderilemedi: tüm SMTP denemeleri başarısız.", lastError);
        }

        private static MailMessage BuildMessage(string to, string title, string password)
        {
            var fromAddress = new MailAddress("kaanemmedik@gmail.com", "Emmedik Mehmet Kaan 11-409");
            var recipient = new MailAddress(to);

            var message = new MailMessage(fromAddress, recipient)
            {
                Subject = title,
                Body =
$@"<html>
  <body style='font-family: Arial, sans-serif; color: #333;'>
    <h2 style='color:#2e6c80;'>Здравствуйте!</h2>
    <p>Вы успешно авторизовались на сайте.</p>
    <p>Ваши данные для входа:</p>
    <ul>
      <li><b>Логин:</b> {WebUtility.HtmlEncode(to)}</li>
      <li><b>Пароль:</b> {WebUtility.HtmlEncode(password)}</li>
    </ul>
  </body>
</html>",
                IsBodyHtml = true
            };

            var path = Path.Combine(Directory.GetCurrentDirectory(), "HomeWork_4.zip");
            if (File.Exists(path))
            {
                message.Attachments.Add(new Attachment(path));
            }
            else
            {
                Console.WriteLine($"[INFO] Ek bulunamadı, eklenmeyecek: {path}");
            }

            return message;
        }
    }
}
