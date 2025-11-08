using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiniHttpServer.Server;
using MiniHttpServer.Settings;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (!cts.IsCancellationRequested)
            {
                Console.WriteLine("Получен сигнал прерывания (Ctrl+C). Останавливаем сервер...");
                cts.Cancel();
            }
        };

        await Task.Run(() =>
        {
            try
            {
                var settings = Singleton.GetInstance().Settings;

                if (settings == null)
                {
                    Console.WriteLine("Ошибка: настройки не загружены");
                    return;
                }

                if (!File.Exists("Public/index.html"))
                {
                    Console.WriteLine("Файл Public/index.html не найден");
                }

                var server = new HttpServer(settings);

                Console.WriteLine($"Запуск сервера на {settings.Domain}:{settings.Port}");
                server.Start(token);

                Console.WriteLine("Сервер запущен. Введите '/stop' для остановки");

                while (!token.IsCancellationRequested)
                {
                    var input = Console.ReadLine();
                    if (string.Equals(input, "/stop", StringComparison.OrdinalIgnoreCase))
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
        }, token);
    }
}
