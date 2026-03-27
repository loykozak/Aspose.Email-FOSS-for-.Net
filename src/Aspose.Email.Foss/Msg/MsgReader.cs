using System.Buffers.Binary;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Aspose.Email.Foss.Cfb;

namespace Aspose.Email.Foss.Msg;

public sealed class MsgReader : IDisposable
{
    private static readonly Regex RecipientNamePattern = new("^__recip_version1\\.0_#[0-9A-Fa-f]{8}$", RegexOptions.Compiled);
    private static readonly Regex AttachmentNamePattern = new("^__attach_version1\\.0_#[0-9A-Fa-f]{8}$", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, LayoutField> TopLevelPropertyStreamLayout = new Dictionary<string, LayoutField>(StringComparer.Ordinal)
    {
        ["reserved_0"] = new(0, 8),
        ["next_recipient_id"] = new(8, 4),
        ["next_attachment_id"] = new(12, 4),
        ["recipient_count"] = new(16, 4),
        ["attachment_count"] = new(20, 4),
        ["reserved_1"] = new(24, 8),
    };

    private static readonly IReadOnlyDictionary<string, LayoutField> SubobjectPropertyStreamLayout = new Dictionary<string, LayoutField>(StringComparer.Ordinal)
    {
        ["reserved_0"] = new(0, 8),
    };

    private static readonly IReadOnlyDictionary<string, LayoutField> FixedLengthEntryLayout = new Dictionary<string, LayoutField>(StringComparer.Ordinal)
    {
        ["property_tag"] = new(0, 4),
        ["flags"] = new(4, 4),
        ["value"] = new(8, 8),
    };

    private readonly CfbReader _cfbReader;
    private readonly bool _strict;
    private bool _closed;
    private readonly List<string> _validationIssues = [];
    private readonly StorageLayout _storageLayout;
    private readonly IReadOnlyList<PropertyEntryFixedLength> _topLevelFixedEntries;

    public IReadOnlyList<string> ValidationIssues => new ReadOnlyCollection<string>(_validationIssues);

    public static MsgReader FromFile(string path, bool strict = false) => new(Aspose.Email.Foss.Cfb.CfbReader.FromFile(path), strict);

    public static MsgReader FromStream(Stream stream, bool strict = false) => new(Aspose.Email.Foss.Cfb.CfbReader.FromStream(stream), strict);

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _cfbReader.Dispose();
        _closed = true;
    }

    internal MsgReader(CfbReader cfbReader, bool strict = false)
    {
        _cfbReader = cfbReader ?? throw new ArgumentNullException(nameof(cfbReader));
        _strict = strict;
        _storageLayout = BuildStorageLayout();
        var topLevelData = _cfbReader.GetStreamData(_storageLayout.TopLevelPropertyStream.StreamId);
        (TopLevelHeader, _topLevelFixedEntries) = ParseTopLevelPropertyStream(topLevelData);
        ValidateTopLevelCounts();
        ValidateEmbeddedMessageRules();
    }

    internal CfbReader CfbReader => !_closed ? _cfbReader : throw new MsgException("MsgReader is closed.");

    internal bool Strict => _strict;

    internal StorageLayout StorageLayout => _storageLayout;

    internal PropertyStreamHeaderTopLevel TopLevelHeader { get; }

    internal IEnumerable<PropertyEntryFixedLength> IterTopLevelFixedLengthProperties()
    {
        foreach (var entry in _topLevelFixedEntries)
        {
            yield return entry;
        }
    }

    internal IEnumerable<DirectoryEntry> IterRecipientStorages()
    {
        foreach (var entry in _storageLayout.RecipientStorages)
        {
            yield return entry;
        }
    }

    internal IEnumerable<DirectoryEntry> IterAttachmentStorages()
    {
        foreach (var entry in _storageLayout.AttachmentStorages)
        {
            yield return entry;
        }
    }

    internal (PropertyStreamHeaderTopLevel Header, IReadOnlyList<PropertyEntryFixedLength> Entries) ParseMessagePropertyStream(uint storageStreamId = CfbConstants.RootStreamId)
    {
        byte[] data;
        if (storageStreamId == CfbConstants.RootStreamId)
        {
            data = _cfbReader.GetStreamData(_storageLayout.TopLevelPropertyStream.StreamId);
        }
        else
        {
            data = _cfbReader.GetStreamData(GetSinglePropertyStreamEntry(storageStreamId).StreamId);
        }

        return ParseTopLevelPropertyStream(data);
    }

    internal (PropertyStreamHeaderSubobject Header, IReadOnlyList<PropertyEntryFixedLength> Entries) ParseSubobjectPropertyStream(uint storageStreamId)
    {
        var propertyStream = GetSinglePropertyStreamEntry(storageStreamId);
        return ParseSubobjectPropertyStreamData(_cfbReader.GetStreamData(propertyStream.StreamId));
    }

    internal static (PropertyStreamHeaderTopLevel Header, IReadOnlyList<PropertyEntryFixedLength> Entries) ParseTopLevelPropertyStream(byte[] data)
    {
        if (data.Length < 32)
        {
            throw new MsgException("Top-level property stream is smaller than 32-byte header.");
        }

        if ((data.Length - 32) % 16 != 0)
        {
            throw new MsgException($"Top-level property payload length {data.Length - 32} is not divisible by 16 with 32-byte header.");
        }

        var header = new PropertyStreamHeaderTopLevel(
            data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "reserved_0").Offset, 8).ToArray(),
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "next_recipient_id").Offset, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "next_attachment_id").Offset, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "recipient_count").Offset, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "attachment_count").Offset, 4)),
            data.AsSpan(GetLayout(TopLevelPropertyStreamLayout, "reserved_1").Offset, 8).ToArray());

        return (header, ParseFixedLengthEntries(data.AsSpan(32).ToArray(), "top-level"));
    }

    internal static (PropertyStreamHeaderSubobject Header, IReadOnlyList<PropertyEntryFixedLength> Entries) ParseSubobjectPropertyStreamData(byte[] data)
    {
        if (data.Length < 8)
        {
            throw new MsgException("Subobject property stream is smaller than 8-byte header.");
        }

        var header = new PropertyStreamHeaderSubobject(data.AsSpan(GetLayout(SubobjectPropertyStreamLayout, "reserved_0").Offset, 8).ToArray());
        return (header, ParseFixedLengthEntries(data.AsSpan(8).ToArray(), "subobject"));
    }

    private static IReadOnlyList<PropertyEntryFixedLength> ParseFixedLengthEntries(byte[] payload, string context)
    {
        if (payload.Length % 16 != 0)
        {
            throw new MsgException($"{context} property payload length {payload.Length} is not divisible by 16.");
        }

        var entries = new List<PropertyEntryFixedLength>(payload.Length / 16);
        for (var offset = 0; offset < payload.Length; offset += 16)
        {
            entries.Add(new PropertyEntryFixedLength(
                BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + GetLayout(FixedLengthEntryLayout, "property_tag").Offset, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + GetLayout(FixedLengthEntryLayout, "flags").Offset, 4)),
                payload.AsSpan(offset + GetLayout(FixedLengthEntryLayout, "value").Offset, 8).ToArray()));
        }

        return entries;
    }

    private StorageLayout BuildStorageLayout()
    {
        var children = _cfbReader.IterChildren(CfbConstants.RootStreamId).ToArray();
        var namedPropertyMappingStorages = children.Where(child => child.IsStorage() && child.Name == MsgConstants.NamedPropertyMappingStorageName).ToArray();
        if (namedPropertyMappingStorages.Length != 1)
        {
            throw new MsgException($"Expected exactly one top-level named property mapping storage, found {namedPropertyMappingStorages.Length}");
        }

        var topLevelPropertyStreams = children.Where(child => child.IsStream() && child.Name == MsgConstants.PropertyStreamName).ToArray();
        if (topLevelPropertyStreams.Length != 1)
        {
            throw new MsgException($"Expected exactly one top-level property stream, found {topLevelPropertyStreams.Length}");
        }

        var recipientStorages = children.Where(child => child.IsStorage() && RecipientNamePattern.IsMatch(child.Name)).ToArray();
        var attachmentStorages = children.Where(child => child.IsStorage() && AttachmentNamePattern.IsMatch(child.Name)).ToArray();

        foreach (var child in children)
        {
            if (child.IsStorage() && child.Name.StartsWith("__recip_version1.0_#", StringComparison.Ordinal) && !RecipientNamePattern.IsMatch(child.Name))
            {
                throw new MsgException($"Invalid recipient storage naming pattern: {child.Name}");
            }

            if (child.IsStorage() && child.Name.StartsWith("__attach_version1.0_#", StringComparison.Ordinal) && !AttachmentNamePattern.IsMatch(child.Name))
            {
                throw new MsgException($"Invalid attachment storage naming pattern: {child.Name}");
            }
        }

        if ((uint)recipientStorages.Length > MsgConstants.MaxRecipientStorages)
        {
            throw new MsgException($"Recipient storage count exceeds {MsgConstants.MaxRecipientStorages}.");
        }

        if ((uint)attachmentStorages.Length > MsgConstants.MaxAttachmentStorages)
        {
            throw new MsgException($"Attachment storage count exceeds {MsgConstants.MaxAttachmentStorages}.");
        }

        return new StorageLayout(recipientStorages, attachmentStorages, namedPropertyMappingStorages[0], topLevelPropertyStreams[0]);
    }

    private void ValidateTopLevelCounts()
    {
        if ((uint)_storageLayout.RecipientStorages.Count != TopLevelHeader.RecipientCount)
        {
            var message = $"Recipient storage count mismatch: {_storageLayout.RecipientStorages.Count} != {TopLevelHeader.RecipientCount}";
            if (_strict)
            {
                throw new MsgException(message);
            }

            _validationIssues.Add(message);
        }

        if ((uint)_storageLayout.AttachmentStorages.Count != TopLevelHeader.AttachmentCount)
        {
            var message = $"Attachment storage count mismatch: {_storageLayout.AttachmentStorages.Count} != {TopLevelHeader.AttachmentCount}";
            if (_strict)
            {
                throw new MsgException(message);
            }

            _validationIssues.Add(message);
        }
    }

    private void ValidateEmbeddedMessageRules()
    {
        foreach (var attachment in _storageLayout.AttachmentStorages)
        {
            var embeddedStorage = _cfbReader.FindChildByName(attachment.StreamId, MsgConstants.EmbeddedMessageStorageName);
            if (embeddedStorage is null)
            {
                continue;
            }

            if (!embeddedStorage.IsStorage())
            {
                throw new MsgException("Embedded message entry exists but is not a storage object.");
            }

            var embeddedNameId = _cfbReader.FindChildByName(embeddedStorage.StreamId, MsgConstants.NamedPropertyMappingStorageName);
            if (embeddedNameId is not null && !embeddedNameId.IsStorage())
            {
                throw new MsgException("Embedded named property mapping entry exists but is not a storage object.");
            }
        }
    }

    private DirectoryEntry GetSinglePropertyStreamEntry(uint storageStreamId)
    {
        var propertyStreams = _cfbReader.IterChildren(storageStreamId)
            .Where(child => child.IsStream() && child.Name == MsgConstants.PropertyStreamName)
            .ToArray();
        if (propertyStreams.Length != 1)
        {
            throw new MsgException($"Expected one property stream in storage sid={storageStreamId}, found {propertyStreams.Length}");
        }

        return propertyStreams[0];
    }

    private static LayoutField GetLayout(IReadOnlyDictionary<string, LayoutField> map, string name) => map[name];

    private readonly record struct LayoutField(int Offset, int Size);
}
