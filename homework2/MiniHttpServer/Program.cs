using MiniHttpServer;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;

public class Program
{
    public static async Task Main(string[] args)
    {
        SettingsModel? settings = null;
        string? fs = null;

        using var cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        await Task.Run(() =>
        {
            try
            {
                fs = File.ReadAllText("settings.json");
                settings = JsonSerializer.Deserialize<SettingsModel>(fs);

                if (settings is null)
                {
                    Console.WriteLine("Ошибка: настройки не загружены");
                    return;
                }

                if (!File.Exists(settings.SearcherPath))
                    Console.WriteLine($"Файл {settings.SearcherPath} не найден");
                if (!File.Exists(settings.ChatGPTPath))
                    Console.WriteLine($"Файл {settings.ChatGPTPath} не найден");
                if (!File.Exists("Public/index.html"))
                    Console.WriteLine($"Файл Public/index.html не найден");

                var searcherurl = $"http://{settings.Domain}:{settings.Port}/{settings.SearcherUri}/";
                var chatgpturl = $"http://{settings.Domain}:{settings.Port}/{settings.ChatGPTUri}/";

                var server = new HttpServer(settings);

                Console.WriteLine($"Запуск сервера на {settings.Domain}:{settings.Port}");
                server.Start(token);

                Console.WriteLine("Сервер запущен. Введите '/stop' для остановки");
                while (!token.IsCancellationRequested)
                {
                    var input = Console.ReadLine();
                    if (input != null && input.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Получена команда остановки...");
                        cts.Cancel();
                        break;
                    }
                }

                server.Stop();
                Console.WriteLine("Сервер остановлен");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Файл settings.json не найден");
            }
            catch (JsonException)
            {
                Console.WriteLine("Ошибка формата JSON");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Неожиданная ошибка: {ex.Message}");
            }
        });
    }
}