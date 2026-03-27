using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class MultipartParser
{
    public static IReadOnlyList<byte[]> SplitParts(byte[] bodyBytes, string boundary)
    {
        ArgumentNullException.ThrowIfNull(bodyBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);

        var boundaryLine = $"--{boundary}";
        var closingBoundaryLine = $"{boundaryLine}--";
        var parts = new List<byte[]>();
        var activePartStart = -1;
        foreach (var (line, lineStart, nextLineStart) in EnumerateLines(bodyBytes))
        {
            if (!line.SequenceEqualAscii(boundaryLine) && !line.SequenceEqualAscii(closingBoundaryLine))
            {
                continue;
            }

            if (activePartStart >= 0 && lineStart >= activePartStart)
            {
                var partBytes = TrimTrailingLineBreaks(bodyBytes[activePartStart..lineStart]);
                if (partBytes.Length > 0)
                {
                    parts.Add(partBytes);
                }
            }

            if (line.SequenceEqualAscii(closingBoundaryLine))
            {
                activePartStart = -1;
                break;
            }

            activePartStart = nextLineStart;
        }

        return parts;
    }

    private static IEnumerable<(byte[] Line, int LineStart, int NextLineStart)> EnumerateLines(byte[] bytes)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            var lineStart = index;
            while (index < bytes.Length && bytes[index] is not ((byte)'\r') and not ((byte)'\n'))
            {
                index++;
            }

            var line = bytes[lineStart..index];
            if (index < bytes.Length && bytes[index] == (byte)'\r')
            {
                index++;
            }

            if (index < bytes.Length && bytes[index] == (byte)'\n')
            {
                index++;
            }

            yield return (line, lineStart, index);
        }
    }

    private static byte[] TrimTrailingLineBreaks(byte[] value)
    {
        var length = value.Length;
        while (length > 0 && value[length - 1] is (byte)'\r' or (byte)'\n')
        {
            length--;
        }

        return length == value.Length ? value : value[..length];
    }

    private static bool SequenceEqualAscii(this ReadOnlySpan<byte> value, string text)
    {
        var encoded = Encoding.ASCII.GetBytes(text);
        return value.SequenceEqual(encoded);
    }
}
