using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class CharsetDecoder
{
    static CharsetDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string DecodeText(byte[] value, string? charset)
    {
        var encoding = GetEncoding(charset);
        return encoding.GetString(value).TrimEnd('\0');
    }

    public static byte[] EncodeText(string value, string? charset)
    {
        return GetEncoding(charset).GetBytes(value);
    }

    public static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
