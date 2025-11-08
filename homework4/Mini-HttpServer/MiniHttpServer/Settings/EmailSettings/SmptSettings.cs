using System;

namespace MiniHttpServer.Settings.EmailSettings
{
    public class SmtpSettings
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsValid(out string? error)
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                error = "SMTP host boş olamaz.";
                return false;
            }

            if (Port <= 0 || Port > 65535)
            {
                error = "Geçersiz SMTP portu.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                error = "Kullanıcı adı boş olamaz.";
                return false;
            }

            error = null;
            return true;
        }
        public override string ToString()
        {
            var masked = string.IsNullOrEmpty(Password) ? "(empty)" : new string('*', Math.Min(8, Password.Length));
            return $"{Name}: {Username}@{Host}:{Port}, SSL={(EnableSsl ? "on" : "off")}, Password={masked}";
        }
    }
}
