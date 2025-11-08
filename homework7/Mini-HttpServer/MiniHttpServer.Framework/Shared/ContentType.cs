using System.IO;

namespace MiniHttpServer.Shared
{
    public static class ContentType
    {
        public static string GetContentType(string path)
        {
            var extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();

            return extension switch
            {
                ".html" or ".htm" or ".php" => "text/html; charset=UTF-8",
                ".css" => "text/css; charset=UTF-8",
                ".js" => "application/javascript; charset=UTF-8",
                ".json" => "application/json; charset=UTF-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".txt" => "text/plain; charset=UTF-8",
                ".webp" => "image/webp",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".eot" => "application/vnd.ms-fontobject",
                ".otf" => "font/otf",
                _ => "text/html; charset=UTF-8"
            };
        }
    }
}