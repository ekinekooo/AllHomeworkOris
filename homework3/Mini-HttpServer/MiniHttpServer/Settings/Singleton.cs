using System.IO;
using System.Text.Json;

namespace MiniHttpServer.Settings
{
    internal sealed class Singleton
    {
        private static Singleton? _instance;
        public JsonEntity Settings { get; }

        private Singleton()
        {
            var json = File.ReadAllText("Settings/settings.json");
            Settings = JsonSerializer.Deserialize<JsonEntity>(json)!;
        }

        public static Singleton GetInstance()
        {
            return _instance ??= new Singleton();
        }
    }
}
