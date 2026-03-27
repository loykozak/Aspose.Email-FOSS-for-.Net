using System.Globalization;

namespace Aspose.Email.Foss.Msg.Mime;

internal static class EmlMessageMapper
{
    public static MapiMessage ToMapiMessage(MimeMessageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var message = new MapiMessage();
        message.Subject = model.Subject;
        var sender = model.From.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sender.Address))
        {
            sender = model.Sender.FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(sender.Address))
        {
            message.SenderAddressType = "SMTP";
            message.SenderEmailAddress = sender.Address;
            message.SenderName = sender.DisplayName;
        }

        AddRecipients(message, model.To, MapiMessage.RecipientTypeTo);
        AddRecipients(message, model.Cc, MapiMessage.RecipientTypeCc);
        AddRecipients(message, model.Bcc, MapiMessage.RecipientTypeBcc);

        if (!string.IsNullOrWhiteSpace(model.MessageId))
        {
            message.InternetMessageId = model.MessageId;
        }

        if (model.Date is { } date)
        {
            message.MessageDeliveryTime = date.UtcDateTime;
        }

        ExtractContent(model.RootEntity, message);
        return message;
    }

    public static MimeMessageModel ToMimeMessage(MapiMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var boundaryFactory = new MimeBoundaryFactory();
        var rootEntity = BuildRootEntity(message, boundaryFactory);
        var headers = new List<MimeHeader>();
        AddHeader(headers, "MIME-Version", "1.0");
        AddHeader(headers, "Subject", message.Subject);
        var sender = CreateSender(message);
        if (sender is { } senderMailbox)
        {
            AddHeader(headers, "From", AddressParser.FormatSingle(senderMailbox));
        }

        AddHeader(headers, "To", FormatRecipients(message, MapiMessage.RecipientTypeTo));
        AddHeader(headers, "Cc", FormatRecipients(message, MapiMessage.RecipientTypeCc));
        AddHeader(headers, "Bcc", FormatRecipients(message, MapiMessage.RecipientTypeBcc));
        if (message.MessageDeliveryTime is { } deliveryTime)
        {
            AddHeader(headers, "Date", new DateTimeOffset(deliveryTime.ToUniversalTime(), TimeSpan.Zero).ToString("ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture));
        }

        AddHeader(headers, "Message-ID", message.InternetMessageId);
        foreach (var header in rootEntity.Headers)
        {
            headers.Add(header);
        }

        return new MimeMessageModel(headers, rootEntity);
    }

    private static void ExtractContent(MimeEntity entity, MapiMessage message)
    {
        switch (entity)
        {
            case MimeMultipart multipart:
                foreach (var child in multipart.Children)
                {
                    ExtractContent(child, message);
                }

                break;

            case MimePart part:
                if (string.Equals(part.MediaType, "text/plain", StringComparison.OrdinalIgnoreCase) && !part.IsAttachment && string.IsNullOrEmpty(message.Body))
                {
                    message.Body = part.GetText();
                    break;
                }

                if (string.Equals(part.MediaType, "text/html", StringComparison.OrdinalIgnoreCase) && !part.IsAttachment && string.IsNullOrEmpty(message.HtmlBody))
                {
                    message.HtmlBody = part.GetText();
                    break;
                }

                if (string.Equals(part.MediaType, "message/rfc822", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var embedded = ToMapiMessage(new MimeReader().Read(part.DecodedBody));
                        message.Attachments.Add(new MapiAttachment
                        {
                            Filename = part.FileName ?? "message.eml",
                            MimeType = part.ContentType,
                            ContentId = part.ContentId,
                            AttachMethod = MapiMessage.AttachMethodEmbedded,
                            EmbeddedMessage = embedded,
                        });
                        break;
                    }
                    catch
                    {
                    }
                }

                message.Attachments.Add(new MapiAttachment
                {
                    Filename = part.FileName,
                    MimeType = part.ContentType,
                    ContentId = part.ContentId,
                    Data = part.DecodedBody.ToArray(),
                });
                break;
        }
    }

    private static MimeEntity BuildRootEntity(MapiMessage message, MimeBoundaryFactory boundaryFactory)
    {
        var bodyEntity = BuildBodyEntity(message, boundaryFactory);
        var attachmentEntities = message.Attachments.Select(attachment => BuildAttachmentPart(attachment, boundaryFactory)).ToArray();
        if (attachmentEntities.Length == 0)
        {
            return bodyEntity;
        }

        var boundary = boundaryFactory.NextBoundary("mixed");
        var headers = new[]
        {
            new MimeHeader("Content-Type", $"multipart/mixed; boundary=\"{boundary}\""),
        };
        var children = new List<MimeEntity> { bodyEntity };
        children.AddRange(attachmentEntities);
        return new MimeMultipart(headers, boundary, children);
    }

    private static MimeEntity BuildBodyEntity(MapiMessage message, MimeBoundaryFactory boundaryFactory)
    {
        var hasText = !string.IsNullOrEmpty(message.Body);
        var hasHtml = !string.IsNullOrEmpty(message.HtmlBody);
        if (hasText && hasHtml)
        {
            var boundary = boundaryFactory.NextBoundary("alternative");
            var headers = new[]
            {
                new MimeHeader("Content-Type", $"multipart/alternative; boundary=\"{boundary}\""),
            };
            return new MimeMultipart(headers, boundary, new[]
            {
                BuildTextPart("text/plain", message.Body!),
                BuildTextPart("text/html", message.HtmlBody!),
            });
        }

        if (hasHtml)
        {
            return BuildTextPart("text/html", message.HtmlBody!);
        }

        return BuildTextPart("text/plain", message.Body ?? string.Empty);
    }

    private static MimePart BuildTextPart(string mediaType, string text)
    {
        var decodedBody = CharsetDecoder.EncodeText(text, "utf-8");
        var (transferEncoding, encodedBody) = TransferEncodingEncoder.EncodeBody(mediaType, decodedBody);
        var headers = new[]
        {
            new MimeHeader("Content-Type", $"{mediaType}; charset=utf-8"),
            new MimeHeader("Content-Transfer-Encoding", transferEncoding),
        };
        return new MimePart(headers, encodedBody, decodedBody);
    }

    private static MimePart BuildAttachmentPart(MapiAttachment attachment, MimeBoundaryFactory boundaryFactory)
    {
        byte[] decodedBody;
        var mediaType = attachment.MimeType ?? GuessMimeType(attachment.Filename);
        if (attachment.EmbeddedMessage is not null)
        {
            decodedBody = new MimeWriter().Write(ToMimeMessage(attachment.EmbeddedMessage));
            mediaType = "message/rfc822";
        }
        else
        {
            decodedBody = attachment.Data.ToArray();
        }

        var (transferEncoding, encodedBody) = TransferEncodingEncoder.EncodeBody(mediaType, decodedBody);
        var disposition = !string.IsNullOrEmpty(attachment.ContentId) ? "inline" : "attachment";
        var headers = new List<MimeHeader>
        {
            new("Content-Type", string.IsNullOrEmpty(attachment.Filename) ? mediaType : $"{mediaType}; name=\"{attachment.Filename}\""),
            new("Content-Transfer-Encoding", transferEncoding),
            new("Content-Disposition", string.IsNullOrEmpty(attachment.Filename) ? disposition : $"{disposition}; filename=\"{attachment.Filename}\""),
        };

        if (!string.IsNullOrEmpty(attachment.ContentId))
        {
            headers.Add(new MimeHeader("Content-ID", $"<{attachment.ContentId}>"));
        }

        return new MimePart(headers, encodedBody, decodedBody);
    }

    private static void AddRecipients(MapiMessage message, IEnumerable<MimeMailbox> mailboxes, int recipientType)
    {
        foreach (var mailbox in mailboxes)
        {
            if (string.IsNullOrWhiteSpace(mailbox.Address))
            {
                continue;
            }

            message.AddRecipient(mailbox.Address, mailbox.DisplayName, recipientType);
        }
    }

    private static string? FormatRecipients(MapiMessage message, int recipientType)
    {
        var recipients = message.Recipients
            .Where(recipient => recipient.RecipientType == recipientType && !string.IsNullOrWhiteSpace(recipient.EmailAddress))
            .Select(recipient => new MimeMailbox(recipient.DisplayName, recipient.EmailAddress!))
            .ToArray();
        return recipients.Length == 0 ? null : AddressParser.FormatList(recipients);
    }

    private static MimeMailbox? CreateSender(MapiMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.SenderEmailAddress))
        {
            return null;
        }

        return new MimeMailbox(message.SenderName, message.SenderEmailAddress!);
    }

    private static void AddHeader(ICollection<MimeHeader> headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            headers.Add(new MimeHeader(name, value));
        }
    }

    private static string GuessMimeType(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "application/octet-stream";
        }

        return Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".eml" => "message/rfc822",
            _ => "application/octet-stream",
        };
    }
}
