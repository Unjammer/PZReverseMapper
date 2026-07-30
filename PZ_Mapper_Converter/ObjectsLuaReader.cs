using System.Text;
using System.Text.RegularExpressions;

namespace PZ_Mapper_Converter;

internal static class ObjectsLuaReader
{
    public static IReadOnlyList<MapObject> Read(string file)
    {
        var text = File.ReadAllText(file);
        var objects = new List<MapObject>();

        foreach (var block in ExtractObjectBlocks(text))
        {
            var mapObject = ParseObject(block);
            if (mapObject is not null)
            {
                objects.Add(mapObject);
            }
        }

        return objects;
    }

    private static IEnumerable<string> ExtractObjectBlocks(string text)
    {
        var objectsIndex = text.IndexOf("objects", StringComparison.Ordinal);
        var rootStart = text.IndexOf('{', Math.Max(objectsIndex, 0));
        if (rootStart < 0)
        {
            yield break;
        }

        var depth = 0;
        var objectStart = -1;
        var inString = false;
        var quote = '\0';
        var escaped = false;

        for (var i = rootStart; i < text.Length; i++)
        {
            var current = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                inString = true;
                quote = current;
                continue;
            }

            if (current == '{')
            {
                depth++;
                if (depth == 2)
                {
                    objectStart = i;
                }
            }
            else if (current == '}')
            {
                if (depth == 2 && objectStart >= 0)
                {
                    yield return text.Substring(objectStart, i - objectStart + 1);
                    objectStart = -1;
                }

                depth--;
                if (depth <= 0)
                {
                    yield break;
                }
            }
        }
    }

    private static MapObject? ParseObject(string block)
    {
        var type = ReadString(block, "type") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var propertiesTable = ExtractTable(block, "properties");
        var pointsTable = ExtractTable(block, "points");

        return new MapObject
        {
            Name = ReadString(block, "name") ?? string.Empty,
            Type = type,
            X = ReadInt(block, "x"),
            Y = ReadInt(block, "y"),
            Z = ReadInt(block, "z"),
            Width = ReadInt(block, "width"),
            Height = ReadInt(block, "height"),
            Geometry = ReadString(block, "geometry"),
            LineWidth = ReadInt(block, "lineWidth"),
            Points = ParsePoints(pointsTable),
            Properties = ParseProperties(propertiesTable)
        };
    }

    private static string? ReadString(string block, string key)
    {
        var match = Regex.Match(
            block,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}\s*=\s*""((?:\\.|[^""\\])*)""",
            RegexOptions.Singleline);

        return match.Success ? UnescapeLuaString(match.Groups[1].Value) : null;
    }

    private static int? ReadInt(string block, string key)
    {
        var match = Regex.Match(
            block,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}\s*=\s*(-?\d+)",
            RegexOptions.Singleline);

        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static string? ExtractTable(string block, string key)
    {
        var match = Regex.Match(
            block,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}\s*=\s*\{{",
            RegexOptions.Singleline);

        if (!match.Success)
        {
            return null;
        }

        var start = match.Index + match.Length - 1;
        var depth = 0;
        var inString = false;
        var quote = '\0';
        var escaped = false;

        for (var i = start; i < block.Length; i++)
        {
            var current = block[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                inString = true;
                quote = current;
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return block.Substring(start, i - start + 1);
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<ObjectPoint> ParsePoints(string? table)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return Array.Empty<ObjectPoint>();
        }

        var numbers = Regex.Matches(table, @"-?\d+")
            .Select(match => int.Parse(match.Value))
            .ToArray();

        var points = new List<ObjectPoint>(numbers.Length / 2);
        for (var i = 0; i + 1 < numbers.Length; i += 2)
        {
            points.Add(new ObjectPoint(numbers[i], numbers[i + 1]));
        }

        return points;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string? table)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(table) || table.Length < 2)
        {
            return properties;
        }

        var inner = table.Substring(1, table.Length - 2);
        var index = 0;
        while (index < inner.Length)
        {
            SkipSeparators(inner, ref index);
            if (index >= inner.Length)
            {
                break;
            }

            var key = ReadIdentifier(inner, ref index);
            if (string.IsNullOrEmpty(key))
            {
                index++;
                continue;
            }

            SkipWhitespace(inner, ref index);
            if (index >= inner.Length || inner[index] != '=')
            {
                index++;
                continue;
            }

            index++;
            SkipWhitespace(inner, ref index);
            properties[key] = ReadValue(inner, ref index);
        }

        return properties;
    }

    private static void SkipSeparators(string text, ref int index)
    {
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
        {
            index++;
        }
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static string ReadIdentifier(string text, ref int index)
    {
        if (index >= text.Length || !(char.IsLetter(text[index]) || text[index] == '_'))
        {
            return string.Empty;
        }

        var start = index;
        index++;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        return text.Substring(start, index - start);
    }

    private static string ReadValue(string text, ref int index)
    {
        if (index >= text.Length)
        {
            return string.Empty;
        }

        if (text[index] is '"' or '\'')
        {
            return ReadQuotedString(text, ref index);
        }

        if (text[index] == '{')
        {
            return ReadBalancedTable(text, ref index);
        }

        var start = index;
        while (index < text.Length && text[index] != ',')
        {
            index++;
        }

        return text.Substring(start, index - start).Trim();
    }

    private static string ReadQuotedString(string text, ref int index)
    {
        var quote = text[index++];
        var builder = new StringBuilder();
        var escaped = false;

        while (index < text.Length)
        {
            var current = text[index++];
            if (escaped)
            {
                builder.Append(current switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => current
                });
                escaped = false;
            }
            else if (current == '\\')
            {
                escaped = true;
            }
            else if (current == quote)
            {
                break;
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    private static string ReadBalancedTable(string text, ref int index)
    {
        var start = index;
        var depth = 0;
        var inString = false;
        var quote = '\0';
        var escaped = false;

        while (index < text.Length)
        {
            var current = text[index++];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                inString = true;
                quote = current;
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }
        }

        return text.Substring(start, index - start).Trim();
    }

    private static string UnescapeLuaString(string value)
    {
        var index = 0;
        return ReadQuotedString($"\"{value}\"", ref index);
    }
}
