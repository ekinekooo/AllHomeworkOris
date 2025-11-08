using System.Net;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.HttpResponse
{
    public interface IActionResult
    {
        Task ExecuteAsync(HttpListenerContext context);
    }
}
