using System.Text;

namespace Aspose.Email.Foss.Msg.Mime;

internal readonly record struct MimeMailbox(string? DisplayName, string Address);

internal static class AddressParser
{
    public static IReadOnlyList<MimeMailbox> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<MimeMailbox>();
        }

        var items = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var angleDepth = 0;
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    current.Append(ch);
                    break;
                case '<':
                    angleDepth++;
                    current.Append(ch);
                    break;
                case '>':
                    angleDepth = Math.Max(0, angleDepth - 1);
                    current.Append(ch);
                    break;
                case ',' when !inQuotes && angleDepth == 0:
                    items.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        if (current.Length > 0)
        {
            items.Add(current.ToString());
        }

        var mailboxes = new List<MimeMailbox>(items.Count);
        foreach (var item in items)
        {
            var mailbox = ParseSingle(item);
            if (!string.IsNullOrWhiteSpace(mailbox.Address))
            {
                mailboxes.Add(mailbox);
            }
        }

        return mailboxes;
    }

    public static string FormatList(IEnumerable<MimeMailbox> mailboxes)
    {
        return string.Join(", ", mailboxes.Select(FormatSingle));
    }

    public static string FormatSingle(MimeMailbox mailbox)
    {
        if (string.IsNullOrWhiteSpace(mailbox.DisplayName))
        {
            return mailbox.Address;
        }

        return $"{QuoteDisplayName(mailbox.DisplayName)} <{mailbox.Address}>";
    }

    private static MimeMailbox ParseSingle(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return default;
        }

        var colonIndex = trimmed.LastIndexOf(':');
        if (colonIndex >= 0 && !trimmed.Contains('<'))
        {
            trimmed = trimmed[(colonIndex + 1)..].Trim();
        }

        var ltIndex = trimmed.LastIndexOf('<');
        var gtIndex = trimmed.LastIndexOf('>');
        if (ltIndex >= 0 && gtIndex > ltIndex)
        {
            var displayName = trimmed[..ltIndex].Trim().Trim('"');
            var address = trimmed[(ltIndex + 1)..gtIndex].Trim();
            return new MimeMailbox(string.IsNullOrWhiteSpace(displayName) ? null : displayName, address);
        }

        return new MimeMailbox(null, trimmed.Trim('"'));
    }

    private static string QuoteDisplayName(string displayName)
    {
        if (displayName.All(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '.' or '-' or '_'))
        {
            return displayName;
        }

        return $"\"{displayName.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
