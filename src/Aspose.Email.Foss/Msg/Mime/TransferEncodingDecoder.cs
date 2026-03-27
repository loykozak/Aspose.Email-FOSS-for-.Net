using System.Globalization;
using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class TransferEncodingDecoder
{
    public static byte[] Decode(string? transferEncoding, byte[] bodyBytes)
    {
        if (bodyBytes.Length == 0)
        {
            return Array.Empty<byte>();
        }

        return transferEncoding?.Trim().ToLowerInvariant() switch
        {
            "base64" => DecodeBase64(Encoding.ASCII.GetString(bodyBytes)),
            "quoted-printable" => DecodeQuotedPrintable(bodyBytes),
            _ => bodyBytes.ToArray(),
        };
    }

    public static byte[] DecodeBase64(string value)
    {
        try
        {
            var normalized = new string(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            return string.IsNullOrEmpty(normalized) ? Array.Empty<byte>() : Convert.FromBase64String(normalized);
        }
        catch
        {
            return Encoding.ASCII.GetBytes(value);
        }
    }

    public static byte[] DecodeQuotedPrintable(byte[] value)
    {
        var output = new List<byte>(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)'=')
            {
                output.Add(value[index]);
                continue;
            }

            if (index + 1 < value.Length && value[index + 1] == (byte)'\n')
            {
                index += 1;
                continue;
            }

            if (index + 2 < value.Length && value[index + 1] == (byte)'\r' && value[index + 2] == (byte)'\n')
            {
                index += 2;
                continue;
            }

            if (index + 2 >= value.Length)
            {
                output.Add(value[index]);
                continue;
            }

            var hex = Encoding.ASCII.GetString(value, index + 1, 2);
            if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var decoded))
            {
                output.Add(decoded);
                index += 2;
                continue;
            }

            output.Add(value[index]);
        }

        return output.ToArray();
    }

    public static byte[] DecodeEncodedWordQuotedPrintable(string value)
    {
        return DecodeQuotedPrintable(Encoding.ASCII.GetBytes(value.Replace('_', ' ')));
    }
}
