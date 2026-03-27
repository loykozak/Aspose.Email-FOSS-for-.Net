using Aspose.Email.Foss.Cfb;

namespace Aspose.Email.Foss.Msg;

internal sealed record StorageLayout(
    IReadOnlyList<DirectoryEntry> RecipientStorages,
    IReadOnlyList<DirectoryEntry> AttachmentStorages,
    DirectoryEntry NamedPropertyMappingStorage,
    DirectoryEntry TopLevelPropertyStream);
