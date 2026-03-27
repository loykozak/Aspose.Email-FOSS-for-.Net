# Public API

Stable namespaces:

- `Aspose.Email.Foss.Cfb`
- `Aspose.Email.Foss.Msg`

Primary public types:

- `CfbConstants`
- `CfbDocument`
- `CfbException`
- `CfbReader`
- `CfbStorage`
- `CfbStream`
- `CfbWriter`
- `DirectoryColorFlag`
- `DirectoryEntry`
- `DirectoryEntryNameComparer`
- `DirectoryObjectType`
- `Header`
- `SectorMarker`
- `CommonMessagePropertyId`
- `MapiAttachment`
- `MapiMessage`
- `MapiProperty`
- `MapiPropertyCollection`
- `MapiRecipient`
- `MsgConstants`
- `MsgDocument`
- `MsgException`
- `MsgReader`
- `MsgStorage`
- `MsgStorageRole`
- `MsgStream`
- `MsgWriter`
- `PropertyTypeCode`

High-level message workflows:

- `MapiMessage.Create`
- `MapiMessage.FromFile`
- `MapiMessage.FromStream`
- `MapiMessage.FromMsgDocument`
- `MapiMessage.LoadFromEml`
- `MapiMessage.Save`
- `MapiMessage.SaveToEml`
- `MapiMessage.AddRecipient`
- `MapiMessage.AddAttachment`
- `MapiMessage.AddEmbeddedMessageAttachment`

Stream-capable entry points:

- `CfbReader.FromStream`
- `CfbDocument.FromStream`
- `MsgReader.FromStream`
- `MsgDocument.FromStream`
- `MapiMessage.FromStream`
- `MapiMessage.LoadFromEml(Stream)`
- `MapiMessage.Save(Stream)`
- `MapiMessage.SaveToEml(Stream)`
- `MapiMessage.AddAttachment(string, Stream, string?, string?)`
- `MapiAttachment.FromStream`
- `MapiAttachment.LoadData`
- `MapiAttachment.OpenRead`
