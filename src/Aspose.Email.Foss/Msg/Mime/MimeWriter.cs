using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal sealed class MimeWriter
{
    public byte[] Write(MimeMessageModel message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var memory = new MemoryStream();
        Write(memory, message);
        return memory.ToArray();
    }

    public void Write(Stream stream, MimeMessageModel message)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        var headerBytes = SerializeHeaders(message.Headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write("\r\n"u8);
        var bodyBytes = SerializeEntityBody(message.RootEntity);
        stream.Write(bodyBytes, 0, bodyBytes.Length);
    }

    private static byte[] SerializeEntity(MimeEntity entity)
    {
        using var memory = new MemoryStream();
        var headers = SerializeHeaders(entity.Headers);
        memory.Write(headers, 0, headers.Length);
        memory.Write("\r\n"u8);
        var body = SerializeEntityBody(entity);
        memory.Write(body, 0, body.Length);
        return memory.ToArray();
    }

    private static byte[] SerializeEntityBody(MimeEntity entity)
    {
        return entity switch
        {
            MimeMultipart multipart => SerializeMultipartBody(multipart),
            MimePart part => SerializePartBody(part),
            _ => Array.Empty<byte>(),
        };
    }

    private static byte[] SerializePartBody(MimePart part)
    {
        var (_, encodedBody) = TransferEncodingEncoder.EncodeBody(part.MediaType, part.DecodedBody);
        return NormalizeTrailingLineEnding(encodedBody);
    }

    private static byte[] SerializeMultipartBody(MimeMultipart multipart)
    {
        using var memory = new MemoryStream();
        foreach (var child in multipart.Children)
        {
            var marker = Encoding.ASCII.GetBytes($"--{multipart.Boundary}\r\n");
            memory.Write(marker, 0, marker.Length);
            var serialized = SerializeEntity(child);
            memory.Write(serialized, 0, serialized.Length);
            memory.Write("\r\n"u8);
        }

        var closing = Encoding.ASCII.GetBytes($"--{multipart.Boundary}--\r\n");
        memory.Write(closing, 0, closing.Length);
        return memory.ToArray();
    }

    private static byte[] SerializeHeaders(IEnumerable<MimeHeader> headers)
    {
        var builder = new StringBuilder();
        foreach (var header in headers)
        {
            builder.Append(header.Name);
            builder.Append(": ");
            builder.Append(TransferEncodingEncoder.EncodeHeaderValue(header.Value));
            builder.Append("\r\n");
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] NormalizeTrailingLineEnding(byte[] body)
    {
        if (body.Length == 0)
        {
            return body;
        }

        if (body[^1] == (byte)'\n')
        {
            return body;
        }

        return body.Concat("\r\n"u8.ToArray()).ToArray();
    }
}
