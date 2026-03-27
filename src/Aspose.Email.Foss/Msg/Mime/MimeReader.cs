namespace Aspose.Email.Foss.Msg.Mime;

internal sealed class MimeReader
{
    public MimeMessageModel Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var (headerBytes, bodyBytes) = HeaderParser.SplitHeaderAndBody(bytes);
        var headers = HeaderParser.ParseHeaders(headerBytes);
        var rootEntity = ParseEntity(headers, bodyBytes);
        return new MimeMessageModel(headers, rootEntity);
    }

    public MimeMessageModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray());
    }

    public MimeMessageModel ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Read(File.ReadAllBytes(path));
    }

    private MimeEntity ParseEntity(IReadOnlyList<MimeHeader> headers, byte[] bodyBytes)
    {
        var (contentType, parameters) = HeaderParser.ParseStructuredHeaderValue(
            headers.FirstOrDefault(header => header.NormalizedName == "content-type")?.Value,
            "text/plain");
        if (contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) &&
            parameters.TryGetValue("boundary", out var boundary) &&
            !string.IsNullOrWhiteSpace(boundary))
        {
            var children = MultipartParser.SplitParts(bodyBytes, boundary)
                .Select(ParseRawEntity)
                .ToArray();
            return new MimeMultipart(headers, boundary, children);
        }

        var decodedBody = TransferEncodingDecoder.Decode(
            headers.FirstOrDefault(header => header.NormalizedName == "content-transfer-encoding")?.Value,
            bodyBytes);
        return new MimePart(headers, bodyBytes, decodedBody);
    }

    private MimeEntity ParseRawEntity(byte[] bytes)
    {
        var (headerBytes, bodyBytes) = HeaderParser.SplitHeaderAndBody(bytes);
        var headers = HeaderParser.ParseHeaders(headerBytes);
        return ParseEntity(headers, bodyBytes);
    }
}
