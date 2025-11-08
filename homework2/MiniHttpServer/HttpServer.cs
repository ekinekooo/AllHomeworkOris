using MiniHttpServer;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

public class HttpServer
{
    private HttpListener _listener = new();
    private SettingsModel _config;
    private CancellationToken _token;

    public HttpServer(SettingsModel config) { _config = config; }

    public void Start(CancellationToken token)
    {
        _token = token;

        _listener = new HttpListener();
        string url = $"http://{_config.Domain}:{_config.Port}/";
        _listener.Prefixes.Add(url);

        try
        {
            _listener.Start();
            Console.WriteLine("Сервер запущен! Проверяй в браузере: " + url);
            Receive();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска сервера: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
            Console.WriteLine("Сервер остановлен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка остановки сервера: {ex.Message}");
        }
    }

    private void Receive()
    {
        try
        {
            if (_listener.IsListening && !_token.IsCancellationRequested)
                _listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
        }
        catch (ObjectDisposedException)
        {

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive() ошибка: {ex.Message}");
        }
    }

    private async void ListenerCallback(IAsyncResult result)
    {
        if (!_listener.IsListening || _token.IsCancellationRequested) return;

        HttpListenerContext context;
        try
        {
            context = _listener.EndGetContext(result);
        }
        catch
        {
            return;
        }

        var response = context.Response;
        var request = context.Request;

        try
        {
            string? responseText = null;
            string path = request.Url?.AbsolutePath.Trim('/') ?? string.Empty;


            bool isHead = string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);


            if (path == _config.SearcherUri)
            {
                responseText = GetResponseText(_config.SearcherPath);
            }
            else if (path == _config.ChatGPTUri)
            {
                responseText = GetResponseText(_config.ChatGPTPath);
            }
            else
            {
                if (string.IsNullOrEmpty(path) || path == "index")
                    path = "index.html";

                string filePath = Path.Combine("Public", path);
                if (Path.GetExtension(filePath) == string.Empty)
                    filePath += ".html";

                responseText = GetResponseText(filePath);
            }

            if (responseText == null)
            {
                response.StatusCode = 404;
                responseText = "Ошибка сервера. Страница не найдена";

            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentLength64 = buffer.Length;

            if (!isHead)
            {
                using Stream output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length);
                await output.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки запроса: {ex.Message}");
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
            Console.WriteLine($"Запрос обработан: {request.Url?.AbsolutePath}");
            if (!_token.IsCancellationRequested) Receive();
        }
    }

    public string? GetResponseText(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Файл не найден: {path}");
                return null;
            }

            string content = File.ReadAllText(path, Encoding.UTF8);
            Console.WriteLine($"Успешно загружен файл: {path}");
            return content;
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine("Директория не найдена");
            return null;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Файл не найден: {path}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении файла {path}: {ex.Message}");
            return null;
        }
    }
}