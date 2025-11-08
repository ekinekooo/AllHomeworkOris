using System;
using System.IO;
using System.Linq;

namespace MiniHttpServer.Shared
{
    public static class GetResponseBytes
    {
        public static byte[]? Invoke(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return Path.HasExtension(path)
                ? TryGetFile(path)
                : TryGetFile(path + "/index.html");
        }

        private static byte[]? TryGetFile(string path)
        {
            try
            {

                var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var targetPath = Path.Combine(parts);

                string? found = Directory
                    .EnumerateFiles("Public", Path.GetFileName(path), SearchOption.AllDirectories)
                    .FirstOrDefault(f => f.EndsWith(targetPath, StringComparison.OrdinalIgnoreCase));

                if (found == null)
                    throw new FileNotFoundException(path);

                return File.ReadAllBytes(found);
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Директория не найдена");
                return null;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Файл не найден");
                return null;
            }
            catch (Exception)
            {
                Console.WriteLine("Ошибка при извлечении текста");
                return null;
            }
        }
    }
}
