namespace MiniHttpServer
{
    public class SettingsModel
    {
        public string SearcherPath { get; set; } = string.Empty;
        public string ChatGPTPath { get; set; } = string.Empty;
        public string SearcherUri { get; set; } = string.Empty;
        public string ChatGPTUri { get; set; } = string.Empty;
        public string Domain { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "8080";

        public SettingsModel() { }

        public SettingsModel(
            string searcherPath,
            string chatGPTPath,
            string searcherUri,
            string chatGPTUri,
            string domain,
            string port)
        {
            SearcherPath = searcherPath ?? string.Empty;
            ChatGPTPath = chatGPTPath ?? string.Empty;
            SearcherUri = searcherUri ?? string.Empty;
            ChatGPTUri = chatGPTUri ?? string.Empty;
            Domain = domain ?? "127.0.0.1";
            Port = port ?? "8080";
        }
    }
}