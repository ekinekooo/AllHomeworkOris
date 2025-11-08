using MiniHttpServer.Settings;
using MiniHttpServer.Shared;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace MiniHttpServer.Server
{
    public class HttpServer
    {
        private HttpListener _listener = new();
        private readonly JsonEntity _config;
        private CancellationToken _token;

        public HttpServer(JsonEntity config) { _config = config; }

        public void Start(CancellationToken token)
        {
            _token = token;

            _listener = new HttpListener();
            string url = $"http://{_config.Domain}:{_config.Port}/";
            _listener.Prefixes.Add(url);
            _listener.Start();
            Console.WriteLine("Сервер запущен! Проверяй в браузере: " + url);
            Receive();
        }

        public void Stop()
        {
            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch { /* yut */ }
        }

        private void Receive()
        {
            if (_listener.IsListening && !_token.IsCancellationRequested)
                _listener.BeginGetContext(ListenerCallback, null);
        }

        protected async void ListenerCallback(IAsyncResult result)
        {
            if (!_listener.IsListening || _token.IsCancellationRequested) return;

            HttpListenerContext context;
            try
            {
                context = _listener.EndGetContext(result);
            }
            catch { return; }

            var request = context.Request;
            var response = context.Response;

            try
            {
                byte[]? buffer = null;

                string path = request.Url?.AbsolutePath.Trim('/') ?? string.Empty;
                string contentPathUsed = path;

                if (string.IsNullOrEmpty(path) || path == "/")
                {
                    contentPathUsed = "Public/index.html";
                    buffer = GetResponseBytes.Invoke(contentPathUsed);
                }
                else if (path == _config.SearcherUri)
                {
                    contentPathUsed = "searcher.html";
                    buffer = GetResponseBytes.Invoke(contentPathUsed);
                }
                else if (path == _config.ChatGPTUri)
                {
                    contentPathUsed = "chatgpt.html";
                    buffer = GetResponseBytes.Invoke(contentPathUsed);
                }
                else if (path == _config.OlaraUri)
                {
                    contentPathUsed = "login.html";
                    buffer = GetResponseBytes.Invoke(contentPathUsed);
                }
                else
                {
                    contentPathUsed = path;
                    buffer = GetResponseBytes.Invoke(path);
                }
                response.ContentType = ContentType.GetContentType(contentPathUsed);

                if (buffer == null)
                {
                    response.StatusCode = 404;
                    var errorText = "<html><body>404 - Not Found</body></html>";
                    buffer = Encoding.UTF8.GetBytes(errorText);
                }

                response.ContentLength64 = buffer.Length;

                using Stream output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length);
                await output.FlushAsync();

                if (response.StatusCode == 200)
                    Console.WriteLine($"Запрос обработан: {request.Url?.AbsolutePath} - Status: {response.StatusCode}");
                else
                    Console.WriteLine($"Ошибка запроса: {request.Url?.AbsolutePath} - Status: {response.StatusCode}");
            }
            catch
            {
                try
                {
                    response.StatusCode = 500;
                    var buf = Encoding.UTF8.GetBytes("Внутренняя ошибка сервера");
                    response.ContentLength64 = buf.Length;
                    using var o = response.OutputStream;
                    await o.WriteAsync(buf, 0, buf.Length);
                }
                catch { /* yut */ }
            }
            finally
            {
                if (!_token.IsCancellationRequested) Receive();
            }
        }
    }
}
