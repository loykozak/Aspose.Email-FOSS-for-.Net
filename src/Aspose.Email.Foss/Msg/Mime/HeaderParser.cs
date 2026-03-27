using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class HeaderParser
{
    private static readonly Regex EncodedWordPattern = new(@"=\?([^?]+)\?([bBqQ])\?([^?]*)\?=", RegexOptions.Compiled);

    public static (byte[] HeaderBytes, byte[] BodyBytes) SplitHeaderAndBody(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        for (var index = 0; index < data.Length - 1; index++)
        {
            if (data[index] == (byte)'\r' &&
                index + 3 < data.Length &&
                data[index + 1] == (byte)'\n' &&
                data[index + 2] == (byte)'\r' &&
                data[index + 3] == (byte)'\n')
            {
                return (data[..index], data[(index + 4)..]);
            }

            if (data[index] == (byte)'\n' && data[index + 1] == (byte)'\n')
            {
                return (data[..index], data[(index + 2)..]);
            }
        }

        return (data, Array.Empty<byte>());
    }

    public static IReadOnlyList<MimeHeader> ParseHeaders(byte[] headerBytes)
    {
        var text = Encoding.Latin1.GetString(headerBytes)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = text.Split('\n');
        var logicalLines = new List<string>();
        var current = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && current.Length > 0)
            {
                current.Append(' ');
                current.Append(line.Trim());
                continue;
            }

            if (current.Length > 0)
            {
                logicalLines.Add(current.ToString());
                current.Clear();
            }

            current.Append(line);
        }

        if (current.Length > 0)
        {
            logicalLines.Add(current.ToString());
        }

        var headers = new List<MimeHeader>(logicalLines.Count);
        foreach (var line in logicalLines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = DecodeEncodedWords(line[(separatorIndex + 1)..].Trim());
            headers.Add(new MimeHeader(name, value));
        }

        return headers;
    }

    public static (string MainValue, IReadOnlyDictionary<string, string> Parameters) ParseStructuredHeaderValue(string? value, string? defaultMainValue)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return (defaultMainValue ?? string.Empty, parameters);
        }

        var input = value.Trim();
        var segments = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < input.Length; index++)
        {
            var ch = input[index];
            if (ch == '"' && (index == 0 || input[index - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }

            if (ch == ';' && !inQuotes)
            {
                segments.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            segments.Add(current.ToString());
        }

        var mainValue = segments.Count == 0 ? defaultMainValue ?? string.Empty : segments[0].Trim();
        for (var index = 1; index < segments.Count; index++)
        {
            var segment = segments[index];
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = segment[..equalsIndex].Trim();
            var parameterValue = segment[(equalsIndex + 1)..].Trim();
            if (parameterValue.Length >= 2 && parameterValue[0] == '"' && parameterValue[^1] == '"')
            {
                parameterValue = parameterValue[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
            }

            parameters[key] = DecodeEncodedWords(parameterValue);
        }

        return (mainValue, parameters);
    }

    public static DateTimeOffset? ParseDateHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static string DecodeEncodedWords(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("=?", StringComparison.Ordinal))
        {
            return value;
        }

        return EncodedWordPattern.Replace(value, match =>
        {
            var charset = match.Groups[1].Value;
            var encoding = match.Groups[2].Value;
            var encodedText = match.Groups[3].Value;
            try
            {
                byte[] bytes;
                if (encoding.Equals("B", StringComparison.OrdinalIgnoreCase))
                {
                    bytes = TransferEncodingDecoder.DecodeBase64(encodedText);
                }
                else
                {
                    bytes = TransferEncodingDecoder.DecodeEncodedWordQuotedPrintable(encodedText);
                }

                return CharsetDecoder.DecodeText(bytes, charset);
            }
            catch
            {
                return match.Value;
            }
        });
    }
}
