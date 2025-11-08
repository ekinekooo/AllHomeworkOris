using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniTemplateEngine
{
    public class HtmlTemplateRenderer : IHtmlTemplateRenderer
    {
        public string RenderFromString(string htmlTemplate, object dataModel)
        {
            var data = ToDictionary(dataModel);
            return Render(htmlTemplate, data);
        }

        public string Render(string htmlTemplate, Dictionary<string, object?> dataModel)
        {
            if (htmlTemplate is null) throw new ArgumentNullException(nameof(htmlTemplate));
            if (dataModel is null) throw new ArgumentNullException(nameof(dataModel));

            var result = new StringBuilder();
            int scanIndex = 0;
            int lastSeen = 0;

            for (int i = 0; i < htmlTemplate.Length; i++)
            {
                scanIndex = i;

                if (htmlTemplate[i] == '$')
                {
                    string keyWord = GetKeyWord(htmlTemplate, i, ref lastSeen);

                    if (keyWord.Equals("if", StringComparison.OrdinalIgnoreCase))
                    {
                        string condition = GetCondition(htmlTemplate, lastSeen, ref lastSeen);
                        bool condResult = EvaluateCondition(dataModel, condition);
                        var (ifBody, elseBody) = GetIfBody(htmlTemplate, ref lastSeen);


                        result.Append(Render(condResult ? ifBody : elseBody, dataModel));

                        i = lastSeen;
                        continue;
                    }
                    else if (keyWord.Equals("foreach", StringComparison.OrdinalIgnoreCase))
                    {
                        string clause = GetCondition(htmlTemplate, lastSeen, ref lastSeen);


                        var words = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (words.Length != 4 ||
                            !words[0].Equals("var", StringComparison.OrdinalIgnoreCase) ||
                            !words[2].Equals("in", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Неправильное использование $foreach (ожидается: $foreach(var x in path))");

                        string variableName = words[1];
                        var collectionObj = GetValueByPath(dataModel, words[3]) as IEnumerable
                            ?? throw new InvalidOperationException("Забыли передать данные коллекции для $foreach");

                        string body = GetForeachBody(htmlTemplate, ref lastSeen);

                        foreach (var item in collectionObj)
                        {
                            object? valueForVar = item;

                            var type = item?.GetType();
                            var valProp = type?.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                            if (valProp is not null)
                                valueForVar = valProp.GetValue(item);

                            var localModel = new Dictionary<string, object?>(dataModel, StringComparer.OrdinalIgnoreCase)
                            {
                                [variableName] = valueForVar
                            };

                            result.Append(Render(body, localModel));
                        }

                        i = lastSeen;
                        continue;
                    }
                }


                if (htmlTemplate[i] == '$' && i + 1 < htmlTemplate.Length && htmlTemplate[i + 1] == '{')
                {
                    int end = htmlTemplate.IndexOf('}', i + 2);
                    if (end == -1)
                        throw new InvalidOperationException("Нету закрывающей скобки для плейсхолдера ${...}");

                    string path = htmlTemplate[(i + 2)..end].Trim();
                    var value = GetValueByPath(dataModel, path)
                                ?? throw new InvalidOperationException($"Отсутствует значение в модели по пути '{path}'");

                    result.Append(ConvertToString(value));
                    i = end;
                }
                else
                {
                    result.Append(htmlTemplate[i]);
                }
            }

            return result.ToString();
        }

        private string GetForeachBody(string htmlTemplate, ref int cursor)
        {
            var body = new StringBuilder();
            int depth = 0;

            for (int i = cursor; i < htmlTemplate.Length; i++)
            {
                if (htmlTemplate[i] == '$')
                {
                    string kw = GetKeyWord(htmlTemplate, i, ref cursor);
                    if (kw.Equals("foreach", StringComparison.OrdinalIgnoreCase))
                    {
                        depth++;
                    }
                    else if (kw.Equals("endfor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (depth == 0)
                            return body.ToString();
                        depth--;
                    }
                }

                body.Append(htmlTemplate[i]);
                cursor = i;
            }

            throw new InvalidOperationException("Не найден $endfor");
        }

        private (string ifBody, string elseBody) GetIfBody(string htmlTemplate, ref int cursor)
        {
            var ifBody = new StringBuilder();
            var elseBody = new StringBuilder();

            bool inElse = false;
            int depth = 0;

            for (int i = cursor; i < htmlTemplate.Length; i++)
            {
                if (htmlTemplate[i] == '$')
                {
                    string kw = GetKeyWord(htmlTemplate, i, ref cursor);

                    if (kw.Equals("if", StringComparison.OrdinalIgnoreCase))
                    {
                        depth++;
                    }
                    else if (kw.Equals("endif", StringComparison.OrdinalIgnoreCase))
                    {
                        if (depth == 0)
                            return (ifBody.ToString(), elseBody.ToString());
                        depth--;
                    }
                    else if (kw.Equals("else", StringComparison.OrdinalIgnoreCase) && depth == 0)
                    {
                        inElse = true;
                        i = cursor;
                        continue;
                    }
                }

                if (inElse) elseBody.Append(htmlTemplate[i]);
                else ifBody.Append(htmlTemplate[i]);

                cursor = i;
            }

            throw new InvalidOperationException("Не найден $endif");
        }

        private string GetCondition(string htmlTemplate, int startIndex, ref int lastSeen)
        {
            int open = htmlTemplate.IndexOf('(', startIndex);
            if (open < 0) throw new InvalidOperationException("Ожидается '(' в условии");

            int close = FindMatchingParen(htmlTemplate, open);
            if (close < 0) throw new InvalidOperationException("Ожидается ')' в условии");

            lastSeen = close + 1;
            return htmlTemplate.Substring(open + 1, close - open - 1).Trim();
        }

        private static int FindMatchingParen(string s, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private string GetKeyWord(string htmlTemplate, int startIndex, ref int lastSeen)
        {
            var key = new StringBuilder();
            int i = startIndex + 1;

            while (i < htmlTemplate.Length && char.IsWhiteSpace(htmlTemplate[i])) i++;

            for (; i < htmlTemplate.Length && htmlTemplate[i] != ' ' && htmlTemplate[i] != '('; i++)
            {
                key.Append(htmlTemplate[i]);
                lastSeen = i;
            }
            if (lastSeen < htmlTemplate.Length - 1) lastSeen++;

            return key.ToString();
        }

        private static string ConvertToString(object value)
        {
            return value switch
            {
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value?.ToString() ?? string.Empty
            };
        }

        private Dictionary<string, object?> ToDictionary(object obj)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (obj is null) return result;

            if (obj is Dictionary<string, object?> dict)
                return new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);

            if (obj is IEnumerable enumerable && obj is not string)
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    result[index++.ToString(CultureInfo.InvariantCulture)] = ToDictionary(item!);
                }
                return result;
            }

            var type = obj.GetType();

            if (IsSimpleType(type))
                return new Dictionary<string, object?> { ["value"] = obj };

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = prop.GetValue(obj);
                result[prop.Name] = val is null || IsSimpleType(val.GetType())
                    ? val
                    : ToDictionary(val);
            }

            return result;
        }

        private static bool IsSimpleType(Type type) =>
            type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime);

        public static object? GetValueByPath(Dictionary<string, object?> obj, string path)
        {
            if (obj is null || string.IsNullOrWhiteSpace(path))
                return null;

            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            object? current = obj;

            foreach (var part in parts)
            {
                if (current is Dictionary<string, object?> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                        return null;
                }
                else
                {
                    var prop = current?.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public);
                    current = prop?.GetValue(current);
                }
            }

            return current;
        }

        private bool EvaluateCondition(Dictionary<string, object?> model, string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return false;

            var gt = Regex.Match(condition, @"([^>]+)\s*>\s*([^>]+)");
            if (gt.Success)
            {
                var left = GetValueByPath(model, gt.Groups[1].Value.Trim());
                var right = GetValueByPath(model, gt.Groups[2].Value.Trim());

                if (left is IComparable lc && right is IComparable rc)
                    return lc.CompareTo(rc) > 0;

                return false;
            }
            var value = GetValueByPath(model, condition.Trim());
            return value is bool b && b;
        }

        public string RenderFromFile(string filePath, object dataModel)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу пуст", nameof(filePath));

            var html = File.ReadAllText(filePath, Encoding.UTF8);
            return RenderFromString(html, dataModel);
        }

        public string RenderToFile(string inputFilePath, string outputFilePath, object dataModel)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("Путь к входному файлу пуст", nameof(inputFilePath));
            if (string.IsNullOrWhiteSpace(outputFilePath))
                throw new ArgumentException("Путь к выходному файлу пуст", nameof(outputFilePath));

            var rendered = RenderFromFile(inputFilePath, dataModel);
            var dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputFilePath, rendered, Encoding.UTF8);
            return outputFilePath;
        }
    }
}
