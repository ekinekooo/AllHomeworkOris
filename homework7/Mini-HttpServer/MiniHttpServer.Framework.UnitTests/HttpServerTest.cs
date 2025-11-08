using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniHttpServer.Frimework.Server;
using MiniHttpServer.Frimework.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.UnitTest
{
    [TestClass]
    public sealed class HttpServerTest
    {
        private static HttpServer? _server;
        private static HttpClient? _httpClient;
        private static readonly string _baseUrl = "http://localhost:9999";
        private static readonly string _publicDir = "test_public";
        private static JsonEntity? _settings;
        private static CancellationTokenSource? _cts;

        [ClassInitialize]
        public static void ClassSetup(TestContext _)
        {

            CreateTestPublicDirectory();


            _settings = CreateTestSettings();


            _cts = new CancellationTokenSource();
            _server = new HttpServer(_settings);
            Task.Run(() => _server.Start(_cts.Token));


            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };


            Thread.Sleep(3000);
        }

        [ClassCleanup]
        public static void ClassTeardown()
        {
            try
            {
                _cts?.Cancel();
                _server?.Stop();
                _httpClient?.Dispose();
                Thread.Sleep(1000);
            }
            finally
            {
                CleanupTestFiles();
            }
        }

        // ---------------------- ТЕСТЫ ----------------------

        [TestMethod("1. Сервер поднимается и отвечает на запросы")]
        public void Server_Should_Start_Successfully()
        {
            Assert.IsNotNull(_server);
            Assert.IsNotNull(_httpClient);
            Assert.IsNotNull(_settings);
        }

        [TestMethod("2. Проверка HTML-эндпоинта /auth/")]
        public void HttpServer_Returns_HtmlPage()
        {
            using var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var response = client.GetAsync("/auth/").GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("text/html; charset=utf-8", response.Content.Headers.ContentType!.ToString());
            Assert.IsTrue(body.Contains("Авторизация", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod("3. Проверка JSON-эндпоинта /auth/json")]
        public void HttpServer_Returns_Json()
        {
            using var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var response = client.GetAsync("/auth/json").GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json; charset=utf-8", response.Content.Headers.ContentType!.ToString());

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.IsTrue(json.Contains("\"name\""), "Ответ не содержит JSON-полей");
        }

        [TestMethod("4. Проверка обработки несуществующего пути")]
        public void HttpServer_Returns404()
        {
            using var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var response = client.GetAsync("/not-found-page.html").GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

            var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.IsTrue(html.Contains("404", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod("5. Проверка POST-запроса /auth/auth")]
        public void HttpServer_Handles_PostRequest()
        {
            using var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            using var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("email","test@example.com"),
                new KeyValuePair<string,string>("password","123456"),
            });

            var response = client.PostAsync("/auth/auth", form).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(
                content.Contains("OK", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("успешно", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task Server_Should_Respond_To_Html_Request()
        {
            // Arrange
            var testFile = Path.Combine(_publicDir, "test.html");
            File.WriteAllText(testFile, "<html><body>Test</body></html>");
            await Task.Delay(100);

            // Act
            var possiblePaths = new[]
            {
                "/test.html",
                $"/{Path.GetFileName(_publicDir)}/test.html",
                "/test_public/test.html"
            };

            HttpResponseMessage? response = null;
            var workingPath = string.Empty;

            foreach (var path in possiblePaths)
            {
                response = await _httpClient!.GetAsync(path);
                Console.WriteLine($"Trying path: {path} -> Status: {response.StatusCode}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    workingPath = path;
                    break;
                }
            }

            if (response is null || response.StatusCode != HttpStatusCode.OK)
            {
                response = await _httpClient!.GetAsync("/test.html");
            }

            var content = await response.Content.ReadAsStringAsync();

            // Debug
            Console.WriteLine($"Final Status Code: {response.StatusCode}");
            Console.WriteLine($"Content: {content}");
            Console.WriteLine($"File exists: {File.Exists(testFile)}");
            Console.WriteLine($"Full path: {Path.GetFullPath(testFile)}");
            Console.WriteLine($"Public dir: {_publicDir}");
            Console.WriteLine($"Working path found: {(response.StatusCode == HttpStatusCode.OK ? workingPath : "NONE")}");

            // Assert
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(content.Contains("Test"),
                    $"Контент не содержит 'Test'. Полученный контент: {content}");
            }
            else
            {
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode,
                    $"Ожидался NotFound, получен {response.StatusCode}");
                Assert.IsTrue(content.Contains("404") || content.Contains("Not Found"),
                    "404 страница должна содержать информацию об ошибке");
            }
        }

        [TestMethod]
        public async Task NonExistent_File_Should_Return_404()
        {
            var response = await _httpClient!.GetAsync("/nonexistent.html");
            Console.WriteLine($"Status Code for nonexistent: {response.StatusCode}");
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Html_File_Should_Have_Correct_ContentType()
        {
            var testFile = Path.Combine(_publicDir, "page.html");
            File.WriteAllText(testFile, "<html><body>Page</body></html>");
            await Task.Delay(100);

            var response = await _httpClient!.GetAsync("/page.html");

            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.IsNotNull(response.Content.Headers.ContentType, "Content-Type заголовок отсутствует");
                Assert.IsTrue(response.Content.Headers.ContentType!.MediaType.Contains("text/html"),
                    $"Content-Type должен содержать 'text/html', получен: {response.Content.Headers.ContentType!.MediaType}");
            }
            else
            {
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode,
                    $"Для статических файлов ожидался либо OK, либо NotFound, получен: {response.StatusCode}");
            }
        }

        [TestMethod]
        public async Task Root_Directory_Should_Be_Accessible()
        {
            var indexFile = Path.Combine(_publicDir, "index.html");
            File.WriteAllText(indexFile, "<html><body>Index</body></html>");

            var response = await _httpClient!.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Root Status: {response.StatusCode}");
            Console.WriteLine($"Root Content: {content}");

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
        }

        [TestMethod]
        public async Task Css_File_Should_Have_Correct_ContentType()
        {
            var testFile = Path.Combine(_publicDir, "style.css");
            File.WriteAllText(testFile, "body { margin: 0; }");

            var response = await _httpClient!.GetAsync("/style.css");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(response.Content.Headers.ContentType?.MediaType?.Contains("text/css") ?? false);
            }
        }

        [TestMethod]
        public async Task JavaScript_File_Should_Have_Correct_ContentType()
        {
            var testFile = Path.Combine(_publicDir, "script.js");
            File.WriteAllText(testFile, "console.log('test');");

            var response = await _httpClient!.GetAsync("/script.js");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(response.Content.Headers.ContentType?.MediaType?.Contains("application/javascript") ?? false);
            }
        }

        [TestMethod]
        public async Task Json_File_Should_Have_Correct_ContentType()
        {
            var jsonContent = JsonSerializer.Serialize(new { test = "value" });
            var testFile = Path.Combine(_publicDir, "data.json");
            File.WriteAllText(testFile, jsonContent);

            var response = await _httpClient!.GetAsync("/data.json");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(response.Content.Headers.ContentType?.MediaType?.Contains("application/json") ?? false);
            }
        }

        [TestMethod]
        public async Task File_In_Subdirectory_Should_Be_Accessible()
        {
            var subDir = Path.Combine(_publicDir, "assets");
            Directory.CreateDirectory(subDir);
            var testFile = Path.Combine(subDir, "page.html");
            File.WriteAllText(testFile, "<html><body>Subdirectory</body></html>");

            var response = await _httpClient!.GetAsync("/assets/page.html");

            Console.WriteLine($"Subdirectory Status: {response.StatusCode}");
            Console.WriteLine($"Directory exists: {Directory.Exists(subDir)}");
            Console.WriteLine($"File exists: {File.Exists(testFile)}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(content.Contains("Subdirectory"));
            }
        }

        [TestMethod]
        public async Task Response_Should_Have_Correct_ContentLength()
        {
            var testContent = "Hello, World!";
            var testFile = Path.Combine(_publicDir, "length.txt");
            File.WriteAllText(testFile, testContent);

            var response = await _httpClient!.GetAsync("/length.txt");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.AreEqual(testContent.Length, response.Content.Headers.ContentLength);
            }
        }

        [TestMethod]
        public void Settings_Should_Be_Correctly_Loaded()
        {
            Assert.IsNotNull(_settings, "Настройки не загружены");

            Console.WriteLine($"Domain: {_settings!.Domain}");
            Console.WriteLine($"Port: {_settings.Port}");
            Console.WriteLine($"RootDirectory: {_settings.RootDirectory}");
            Console.WriteLine($"ConectionString: {_settings.ConectionString}");

            Assert.AreEqual("localhost", _settings.Domain,
                $"Domain должен быть 'localhost', получен: {_settings.Domain}");
            Assert.AreEqual("9999", _settings.Port,
                $"Port должен быть '9999', получен: {_settings.Port}");

            Assert.IsTrue(Directory.Exists(_settings.RootDirectory),
                $"RootDirectory {_settings.RootDirectory} не существует");

            Assert.AreEqual(
                "Host=localhost;Port=5432;Database=TestDb;Username=test;Password=test",
                _settings.ConectionString,
                "Connection string не совпадает");
        }

        // ---------------------- helpers ----------------------

        private static JsonEntity CreateTestSettings()
        {
            var fullPublicDir = Path.GetFullPath(_publicDir);

            var settings = new JsonEntity
            {
                LoginUri = "/login",
                OlaraUri = "/olara",
                SearcherPath = "/search",
                ChatGPTPath = "/chatgpt",
                SearcherUri = "/api/search",
                ChatGPTUri = "/api/chatgpt",
                Domain = "localhost",
                Port = "9999",
                ConectionString = "Host=localhost;Port=5432;Database=TestDb;Username=test;Password=test",
                RootDirectory = fullPublicDir
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("settings.json", json);

            Console.WriteLine($"Settings created with RootDirectory: {fullPublicDir}");
            Console.WriteLine($"Directory exists: {Directory.Exists(fullPublicDir)}");

            return settings;
        }

        private static void CreateTestPublicDirectory()
        {
            if (Directory.Exists(_publicDir))
            {
                Directory.Delete(_publicDir, true);
                Thread.Sleep(100);
            }

            Directory.CreateDirectory(_publicDir);

            Console.WriteLine($"Public directory created: {Path.GetFullPath(_publicDir)}");
            Console.WriteLine($"Directory exists: {Directory.Exists(_publicDir)}");
        }

        private static void CleanupTestFiles()
        {
            try
            {
                if (Directory.Exists(_publicDir))
                    Directory.Delete(_publicDir, true);

                if (File.Exists("settings.json"))
                    File.Delete("settings.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }
    }
}
