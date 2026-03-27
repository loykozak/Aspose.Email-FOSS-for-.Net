using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Aspose.Email.Foss.Msg;

public sealed partial class MapiMessage
{
    private static (MapiPropertyCollection Properties, List<MsgStream> ExtraStreams, byte[] HeaderReserved0, byte[] HeaderReserved1) ParsePropertyStorage(
        MsgStorage storage,
        MsgStorageRole role,
        bool unicodeStrings)
    {
        var properties = new MapiPropertyCollection();
        var extraStreams = new List<MsgStream>();
        var directStreams = new Dictionary<(ushort PropertyId, ushort PropertyType), byte[]>();
        var indexedStreams = new Dictionary<(ushort PropertyId, ushort PropertyType), Dictionary<int, byte[]>>();

        foreach (var stream in storage.Streams)
        {
            var directMatch = StreamPropertyPattern.Match(stream.Name);
            if (directMatch.Success)
            {
                var key = (Convert.ToUInt16(directMatch.Groups[1].Value, 16), Convert.ToUInt16(directMatch.Groups[2].Value, 16));
                directStreams[key] = stream.Data.ToArray();
                continue;
            }

            var indexedMatch = IndexedStreamPropertyPattern.Match(stream.Name);
            if (indexedMatch.Success)
            {
                var key = (Convert.ToUInt16(indexedMatch.Groups[1].Value, 16), Convert.ToUInt16(indexedMatch.Groups[2].Value, 16));
                if (!indexedStreams.TryGetValue(key, out var items))
                {
                    items = [];
                    indexedStreams[key] = items;
                }

                items[Convert.ToInt32(indexedMatch.Groups[3].Value, 16)] = stream.Data.ToArray();
            }
        }

        var headerReserved0 = new byte[8];
        var headerReserved1 = new byte[8];
        if (storage.PropertyStreamHeader is PropertyStreamHeaderTopLevel topLevelHeader)
        {
            headerReserved0 = topLevelHeader.Reserved0.ToArray();
            headerReserved1 = topLevelHeader.Reserved1.ToArray();
        }
        else if (storage.PropertyStreamHeader is PropertyStreamHeaderSubobject subobjectHeader)
        {
            headerReserved0 = subobjectHeader.Reserved0.ToArray();
        }

        var usedStreamNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in storage.FixedLengthProperties)
        {
            var propertyId = (ushort)(entry.PropertyTag >> 16);
            var propertyType = (ushort)(entry.PropertyTag & 0xFFFF);
            directStreams.TryGetValue((propertyId, propertyType), out var directStream);
            indexedStreams.TryGetValue((propertyId, propertyType), out var indexedStreamValues);

            var property = new MapiProperty(propertyId, propertyType, DecodePropertyValue(propertyType, entry.Value, directStream, indexedStreamValues, unicodeStrings), entry.Flags)
            {
                RawInlineValue = FixedInlineSizes.ContainsKey(propertyType) ? entry.Value.ToArray() : null,
                RawSizeValue = FixedInlineSizes.ContainsKey(propertyType) ? null : BinaryPrimitives.ReadUInt32LittleEndian(entry.Value.AsSpan(0, 4)),
                RawReservedValue = FixedInlineSizes.ContainsKey(propertyType) ? null : BinaryPrimitives.ReadUInt32LittleEndian(entry.Value.AsSpan(4, 4)),
                RawDirectStream = directStream?.ToArray(),
                RawIndexedStreams = indexedStreamValues is null
                    ? []
                    : indexedStreamValues.OrderBy(item => item.Key).Select(item => (item.Key, item.Value.ToArray())).ToArray(),
                PreserveRaw = true,
            };
            properties.Set(property);

            if (directStream is not null)
            {
                usedStreamNames.Add(ValueStreamName(propertyId, propertyType));
            }

            if (indexedStreamValues is not null)
            {
                foreach (var index in indexedStreamValues.Keys)
                {
                    usedStreamNames.Add(IndexedValueStreamName(propertyId, propertyType, index));
                }
            }
        }

        foreach (var stream in storage.Streams)
        {
            if (stream.Name == MsgConstants.PropertyStreamName || usedStreamNames.Contains(stream.Name))
            {
                continue;
            }

            if (StreamPropertyPattern.IsMatch(stream.Name) || IndexedStreamPropertyPattern.IsMatch(stream.Name))
            {
                extraStreams.Add(CloneStream(stream));
            }
        }

        return (properties, extraStreams, headerReserved0, headerReserved1);
    }

    private static object? DecodePropertyValue(
        ushort propertyType,
        byte[] inlineValue,
        byte[]? directStream,
        Dictionary<int, byte[]>? indexedStreams,
        bool unicodeStrings)
    {
        if (FixedInlineSizes.ContainsKey(propertyType))
        {
            return DecodeInlineValue(propertyType, inlineValue);
        }

        return propertyType switch
        {
            (ushort)PropertyTypeCode.PtypString => DecodeUnicodeString(directStream ?? []),
            (ushort)PropertyTypeCode.PtypString8 => DecodeString8(directStream ?? [], unicodeStrings),
            (ushort)PropertyTypeCode.PtypBinary => (directStream ?? []).ToArray(),
            (ushort)PropertyTypeCode.PtypMultipleString => indexedStreams?.OrderBy(item => item.Key).Select(item => DecodeUnicodeString(item.Value)).ToArray() ?? Array.Empty<string>(),
            (ushort)PropertyTypeCode.PtypMultipleString8 => indexedStreams?.OrderBy(item => item.Key).Select(item => DecodeString8(item.Value, unicodeStrings)).ToArray() ?? Array.Empty<string>(),
            (ushort)PropertyTypeCode.PtypMultipleBinary => indexedStreams?.OrderBy(item => item.Key).Select(item => item.Value.ToArray()).ToArray() ?? Array.Empty<byte[]>(),
            (ushort)PropertyTypeCode.PtypObject => null,
            _ => (directStream ?? inlineValue).ToArray(),
        };
    }

    private static object DecodeInlineValue(ushort propertyType, byte[] raw)
    {
        return propertyType switch
        {
            (ushort)PropertyTypeCode.PtypInteger16 => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0, 2)),
            (ushort)PropertyTypeCode.PtypInteger32 => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0, 4)),
            (ushort)PropertyTypeCode.PtypFloating32 => BitConverter.ToSingle(raw, 0),
            (ushort)PropertyTypeCode.PtypFloating64 => BitConverter.ToDouble(raw, 0),
            (ushort)PropertyTypeCode.PtypCurrency => BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(0, 8)),
            (ushort)PropertyTypeCode.PtypFloatingTime => BitConverter.ToDouble(raw, 0),
            (ushort)PropertyTypeCode.PtypErrorCode => BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0, 4)),
            (ushort)PropertyTypeCode.PtypBoolean => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2)) != 0,
            (ushort)PropertyTypeCode.PtypInteger64 => BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(0, 8)),
            (ushort)PropertyTypeCode.PtypTime => FileTimeToDateTime(BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0, 8))),
            _ => raw.ToArray(),
        };
    }

    private static (MsgStream PropertyStream, List<MsgStream> ValueStreams) SerializePropertyBag(
        MapiPropertyCollection properties,
        MsgStorageRole role,
        byte[] headerReserved0,
        byte[] headerReserved1,
        int? attachmentMethod,
        int recipientCount,
        int attachmentCount,
        int nextRecipientId,
        int nextAttachmentId)
    {
        var propertyStream = new List<byte>();
        if (role is MsgStorageRole.Message or MsgStorageRole.EmbeddedMessage)
        {
            propertyStream.AddRange(PadBytes(headerReserved0, 8));
            propertyStream.AddRange(BitConverter.GetBytes(recipientCount == 0 ? 0u : (uint)nextRecipientId));
            propertyStream.AddRange(BitConverter.GetBytes(attachmentCount == 0 ? 0u : (uint)nextAttachmentId));
            propertyStream.AddRange(BitConverter.GetBytes((uint)recipientCount));
            propertyStream.AddRange(BitConverter.GetBytes((uint)attachmentCount));
            propertyStream.AddRange(PadBytes(headerReserved1, 8));
        }
        else
        {
            propertyStream.AddRange(PadBytes(headerReserved0, 8));
        }

        var valueStreams = new List<MsgStream>();
        foreach (var property in properties.IterProperties())
        {
            propertyStream.AddRange(BitConverter.GetBytes(property.PropertyTag));
            propertyStream.AddRange(BitConverter.GetBytes(property.Flags));

            if (property.PreserveRaw && PropertyHasCompleteRawPayload(property))
            {
                AppendPreservedProperty(propertyStream, valueStreams, property);
                continue;
            }

            if (FixedInlineSizes.ContainsKey(property.PropertyType))
            {
                propertyStream.AddRange(EncodeInlineValue(property.PropertyType, property.Value));
                continue;
            }

            var (streamPayloads, sizeValue, reservedValue) = EncodeStreamProperty(property, role, attachmentMethod);
            propertyStream.AddRange(BitConverter.GetBytes(sizeValue));
            propertyStream.AddRange(BitConverter.GetBytes(reservedValue));
            valueStreams.AddRange(streamPayloads);
        }

        return (new MsgStream(MsgConstants.PropertyStreamName, propertyStream.ToArray()), valueStreams);
    }

    private static bool PropertyHasCompleteRawPayload(MapiProperty property)
    {
        return FixedInlineSizes.ContainsKey(property.PropertyType)
            ? property.RawInlineValue is not null
            : property.RawSizeValue is not null && property.RawReservedValue is not null;
    }

    private static void AppendPreservedProperty(List<byte> propertyStream, List<MsgStream> valueStreams, MapiProperty property)
    {
        if (FixedInlineSizes.ContainsKey(property.PropertyType))
        {
            propertyStream.AddRange(PadBytes(property.RawInlineValue ?? [], 8));
            if (property.RawDirectStream is not null)
            {
                valueStreams.Add(new MsgStream(ValueStreamName(property.PropertyId, property.PropertyType), property.RawDirectStream.ToArray()));
            }

            return;
        }

        propertyStream.AddRange(BitConverter.GetBytes(property.RawSizeValue ?? 0));
        propertyStream.AddRange(BitConverter.GetBytes(property.RawReservedValue ?? 0));
        if (property.RawDirectStream is not null)
        {
            valueStreams.Add(new MsgStream(ValueStreamName(property.PropertyId, property.PropertyType), property.RawDirectStream.ToArray()));
        }

        foreach (var (index, payload) in property.RawIndexedStreams)
        {
            valueStreams.Add(new MsgStream(IndexedValueStreamName(property.PropertyId, property.PropertyType, index), payload.ToArray()));
        }
    }

    private static (List<MsgStream> StreamPayloads, uint SizeValue, uint ReservedValue) EncodeStreamProperty(MapiProperty property, MsgStorageRole role, int? attachmentMethod)
    {
        var streams = new List<MsgStream>();

        if (property.PropertyType == (ushort)PropertyTypeCode.PtypObject)
        {
            var reservedValue = 0u;
            var attachMethodValue = attachmentMethod ?? AttachMethodByValue;
            if (role == MsgStorageRole.Attachment && attachMethodValue == AttachMethodEmbedded)
            {
                reservedValue = 0x01;
            }
            else if (role == MsgStorageRole.Attachment && attachMethodValue == AttachMethodStorage)
            {
                reservedValue = 0x04;
            }

            return (streams, 0xFFFFFFFFu, reservedValue);
        }

        var directPayload = EncodeSingleStreamValue(property.PropertyType, property.Value, false);
        streams.Add(new MsgStream(ValueStreamName(property.PropertyId, property.PropertyType), directPayload));
        var sizeValue = (uint)directPayload.Length;
        if (property.PropertyType == (ushort)PropertyTypeCode.PtypString)
        {
            sizeValue += 2;
        }
        else if (property.PropertyType == (ushort)PropertyTypeCode.PtypString8)
        {
            sizeValue += 1;
        }

        return (streams, sizeValue, 0);
    }

    private static byte[] EncodeInlineValue(ushort propertyType, object? value)
    {
        var buffer = new byte[8];
        switch (propertyType)
        {
            case (ushort)PropertyTypeCode.PtypInteger16:
                BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(0, 2), Convert.ToInt16(value, CultureInfo.InvariantCulture));
                break;
            case (ushort)PropertyTypeCode.PtypInteger32:
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), Convert.ToInt32(value, CultureInfo.InvariantCulture));
                break;
            case (ushort)PropertyTypeCode.PtypFloating32:
                BitConverter.GetBytes(Convert.ToSingle(value, CultureInfo.InvariantCulture)).CopyTo(buffer, 0);
                break;
            case (ushort)PropertyTypeCode.PtypFloating64:
                BitConverter.GetBytes(Convert.ToDouble(value, CultureInfo.InvariantCulture)).CopyTo(buffer, 0);
                break;
            case (ushort)PropertyTypeCode.PtypCurrency:
            case (ushort)PropertyTypeCode.PtypInteger64:
                BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(0, 8), Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case (ushort)PropertyTypeCode.PtypFloatingTime:
                BitConverter.GetBytes(Convert.ToDouble(value, CultureInfo.InvariantCulture)).CopyTo(buffer, 0);
                break;
            case (ushort)PropertyTypeCode.PtypErrorCode:
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), Convert.ToUInt32(value, CultureInfo.InvariantCulture));
                break;
            case (ushort)PropertyTypeCode.PtypBoolean:
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0, 2), Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? (ushort)1 : (ushort)0);
                break;
            case (ushort)PropertyTypeCode.PtypTime:
                BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(0, 8), DateTimeToFileTime(value as DateTime?));
                break;
            default:
                throw new MsgException($"Unsupported fixed inline property type: 0x{propertyType:X4}");
        }

        return buffer;
    }

    private static byte[] EncodeSingleStreamValue(ushort propertyType, object? value, bool includeNullTerminator)
    {
        switch (propertyType)
        {
            case (ushort)PropertyTypeCode.PtypString:
            {
                var payload = Encoding.Unicode.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return includeNullTerminator ? payload.Concat(new byte[] { 0, 0 }).ToArray() : payload;
            }
            case (ushort)PropertyTypeCode.PtypString8:
            {
                var payload = GetString8Encoding().GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return includeNullTerminator ? payload.Concat(new byte[] { 0 }).ToArray() : payload;
            }
            case (ushort)PropertyTypeCode.PtypBinary:
                return value switch
                {
                    null => [],
                    byte[] bytes => bytes.ToArray(),
                    _ => throw new MsgException($"Expected binary payload for PTYP_BINARY, got {value.GetType().FullName}."),
                };
            case (ushort)PropertyTypeCode.PtypObject:
                return [];
            default:
                return value as byte[] ?? [];
        }
    }

    private static string ValueStreamName(ushort propertyId, ushort propertyType) => $"{MsgConstants.PropertyValueStreamPrefix}{propertyId:X4}{propertyType:X4}";

    private static string IndexedValueStreamName(ushort propertyId, ushort propertyType, int index) => $"{MsgConstants.PropertyValueStreamPrefix}{propertyId:X4}{propertyType:X4}-{index:X8}";

    private static int StorageIdFromName(string name)
    {
        var hashIndex = name.LastIndexOf('#');
        return hashIndex >= 0 && hashIndex + 1 < name.Length ? Convert.ToInt32(name[(hashIndex + 1)..], 16) : 0;
    }

    private static int NextStorageId(IEnumerable<int?> ids)
    {
        var values = ids.Where(id => id.HasValue).Select(id => id!.Value).ToArray();
        return values.Length == 0 ? 0 : values.Max() + 1;
    }

    private static byte[] PadBytes(byte[] value, int length)
    {
        var output = new byte[length];
        Buffer.BlockCopy(value, 0, output, 0, Math.Min(length, value.Length));
        return output;
    }

    private static string DecodeUnicodeString(byte[] data) => Encoding.Unicode.GetString(data).TrimEnd('\0');

    private static string DecodeString8(byte[] data, bool unicodeStrings) => (unicodeStrings ? Encoding.UTF8 : GetString8Encoding()).GetString(data).TrimEnd('\0');

    private static Encoding GetString8Encoding()
    {
        try
        {
            return Encoding.GetEncoding(1252);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    private static ulong DateTimeToFileTime(DateTime? value)
    {
        if (value is null)
        {
            return 0;
        }

        var utc = value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime();
        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (ulong)(utc - epoch).Ticks;
    }

    private static object? FileTimeToDateTime(ulong value)
    {
        if (value == 0)
        {
            return null;
        }

        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddTicks((long)value);
    }

    private static string ShortFilename(string filename)
    {
        var name = Path.GetFileName(filename);
        if (name.Length <= 8)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        return $"{stem[..Math.Min(8, stem.Length)]}{extension}";
    }
}
