using System.Text;

namespace Aspose.Email.Foss.Cfb;

internal static class EncodingHelper
{
    public static int GetUtf16ByteCount(string value) => Encoding.Unicode.GetByteCount(value);
}
