namespace Aspose.Email.Foss.Msg.Mime;

internal sealed class MimeMessageModel
{
    public MimeMessageModel(IEnumerable<MimeHeader> headers, MimeEntity rootEntity)
    {
        Headers = headers?.ToArray() ?? throw new ArgumentNullException(nameof(headers));
        RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
    }

    public IReadOnlyList<MimeHeader> Headers { get; }

    public MimeEntity RootEntity { get; }

    public string? Subject => GetHeaderValue("subject");

    public IReadOnlyList<MimeMailbox> From => AddressParser.ParseList(GetHeaderValue("from"));

    public IReadOnlyList<MimeMailbox> Sender => AddressParser.ParseList(GetHeaderValue("sender"));

    public IReadOnlyList<MimeMailbox> To => AddressParser.ParseList(GetHeaderValue("to"));

    public IReadOnlyList<MimeMailbox> Cc => AddressParser.ParseList(GetHeaderValue("cc"));

    public IReadOnlyList<MimeMailbox> Bcc => AddressParser.ParseList(GetHeaderValue("bcc"));

    public string? MessageId => NormalizeMessageId(GetHeaderValue("message-id"));

    public DateTimeOffset? Date => HeaderParser.ParseDateHeader(GetHeaderValue("date"));

    public string? GetHeaderValue(string normalizedName)
    {
        return Headers.FirstOrDefault(header => header.NormalizedName == normalizedName)?.Value;
    }

    private static string? NormalizeMessageId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
