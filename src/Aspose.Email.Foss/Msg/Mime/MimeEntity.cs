namespace Aspose.Email.Foss.Msg.Mime;

internal abstract class MimeEntity
{
    protected MimeEntity(IEnumerable<MimeHeader> headers)
    {
        Headers = headers?.ToArray() ?? throw new ArgumentNullException(nameof(headers));
        var (contentType, contentTypeParameters) = HeaderParser.ParseStructuredHeaderValue(GetHeaderValue("content-type"), "text/plain");
        var (contentDisposition, contentDispositionParameters) = HeaderParser.ParseStructuredHeaderValue(GetHeaderValue("content-disposition"), null);
        ContentType = contentType;
        MediaType = contentType.ToLowerInvariant();
        ContentTypeParameters = contentTypeParameters;
        ContentDisposition = contentDisposition;
        ContentDispositionParameters = contentDispositionParameters;
        TransferEncoding = GetHeaderValue("content-transfer-encoding")?.Trim().ToLowerInvariant();
        Charset = contentTypeParameters.TryGetValue("charset", out var charset) ? charset : null;
        ContentId = NormalizeContentId(GetHeaderValue("content-id"));
        FileName = GetFilename();
    }

    public IReadOnlyList<MimeHeader> Headers { get; }

    public string ContentType { get; }

    public string MediaType { get; }

    public IReadOnlyDictionary<string, string> ContentTypeParameters { get; }

    public string? ContentDisposition { get; }

    public IReadOnlyDictionary<string, string> ContentDispositionParameters { get; }

    public string? TransferEncoding { get; }

    public string? Charset { get; }

    public string? ContentId { get; }

    public string? FileName { get; }

    public string? GetHeaderValue(string normalizedName)
    {
        return Headers.FirstOrDefault(header => header.NormalizedName == normalizedName)?.Value;
    }

    private string? GetFilename()
    {
        if (ContentDispositionParameters.TryGetValue("filename", out var filename))
        {
            return filename;
        }

        if (ContentTypeParameters.TryGetValue("name", out var name))
        {
            return name;
        }

        return null;
    }

    private static string? NormalizeContentId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[^1] == '>')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }
}
