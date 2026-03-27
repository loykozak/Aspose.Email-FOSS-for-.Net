namespace Aspose.Email.Foss.Msg;

public sealed class MapiRecipient
{
    public string? DisplayName { get; set; }

    public string? EmailAddress { get; set; }

    public int RecipientType { get; set; } = MapiMessage.RecipientTypeTo;

    public string AddressType { get; set; } = "SMTP";

    public MapiPropertyCollection Properties { get; } = new();

    internal int? StorageId { get; set; }

    internal IList<MsgStream> ExtraStreams { get; } = [];

    internal IList<MsgStorage> ExtraStorages { get; } = [];

    internal byte[] HeaderReserved0 { get; set; } = new byte[8];
}
