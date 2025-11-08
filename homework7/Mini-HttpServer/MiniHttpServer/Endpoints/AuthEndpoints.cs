using MiniHttpServer.Core.Atributes;
using MiniHttpServer.Services;
using System;
using System.Threading.Tasks;

namespace MiniHttpServer.Endpoints
{
    [Endpoint]
    internal sealed class AuthEndpoint
    {

        private readonly EmailService _emailService = new();


        [HttpGet]
        public string LoginPage()
        {

            return "index.html";
        }


        [HttpPost("auth")]
        public async Task Login(string email, string password)
        {

            Console.WriteLine("Члены");
            await _emailService.SendEmailAsync(email, "Авторизация прошла успешно", password);
        }


        [HttpPost("sendEmail")]
        public void SendEmail(string to, string title, string message)
        {

        }
    }
}
