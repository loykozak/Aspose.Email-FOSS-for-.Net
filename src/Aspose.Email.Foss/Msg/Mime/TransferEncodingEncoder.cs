using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class TransferEncodingEncoder
{
    public static (string TransferEncoding, byte[] EncodedBody) EncodeBody(string mediaType, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return ("7bit", Array.Empty<byte>());
        }

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            if (IsSevenBit(payload))
            {
                return ("7bit", NormalizeLineEndings(payload));
            }

            return ("quoted-printable", EncodeQuotedPrintable(payload));
        }

        return ("base64", EncodeBase64(payload));
    }

    public static string EncodeHeaderValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.All(ch => ch <= 0x7F))
        {
            return value;
        }

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return $"=?utf-8?B?{payload}?=";
    }

    private static bool IsSevenBit(byte[] payload)
    {
        foreach (var b in payload)
        {
            if (b == 9 || b == 10 || b == 13)
            {
                continue;
            }

            if (b < 32 || b > 126)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] NormalizeLineEndings(byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal));
    }

    private static byte[] EncodeBase64(byte[] payload)
    {
        var base64 = Convert.ToBase64String(payload);
        var builder = new StringBuilder(base64.Length + (base64.Length / 76 + 1) * 2);
        for (var offset = 0; offset < base64.Length; offset += 76)
        {
            builder.Append(base64, offset, Math.Min(76, base64.Length - offset));
            builder.Append("\r\n");
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] EncodeQuotedPrintable(byte[] payload)
    {
        var builder = new StringBuilder(payload.Length * 3);
        var lineLength = 0;
        foreach (var b in payload)
        {
            if (b == (byte)'\r')
            {
                continue;
            }

            if (b == (byte)'\n')
            {
                builder.Append("\r\n");
                lineLength = 0;
                continue;
            }

            var token = IsPrintableQuotedPrintableByte(b) ? ((char)b).ToString() : $"={b:X2}";
            if (lineLength + token.Length > 72)
            {
                builder.Append("=\r\n");
                lineLength = 0;
            }

            builder.Append(token);
            lineLength += token.Length;
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static bool IsPrintableQuotedPrintableByte(byte value)
    {
        return value is >= 33 and <= 60 or >= 62 and <= 126 || value == 9 || value == 32;
    }
}
