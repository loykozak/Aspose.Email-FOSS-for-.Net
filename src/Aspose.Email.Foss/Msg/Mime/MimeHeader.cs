namespace Aspose.Email.Foss.Msg.Mime;

internal sealed class MimeHeader
{
    public MimeHeader(string name, string value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NormalizedName = name.Trim().ToLowerInvariant();
        Value = value ?? string.Empty;
    }

    public string Name { get; }

    public string NormalizedName { get; }

    public string Value { get; }
}
