# Examples

This file maps common tasks to example source files.

## Quick Index

- Create a new `.msg`, read it back, and save `.eml`:
  - [create_msg_and_eml.cs](create_msg_and_eml.cs)
- Read low-level MSG and CFB structure:
  - [msg_reader.cs](msg_reader.cs)
- Read a message through the high-level API and print a summary:
  - [msg_summary.cs](msg_summary.cs)

## Example Files

### [create_msg_and_eml.cs](create_msg_and_eml.cs)

Use this example to:
- create a new `MapiMessage`
- set common properties through `CommonMessagePropertyId`
- add recipients
- add attachments
- save as `.msg`
- load the `.msg` back
- save the loaded message as `.eml`

Run:

```powershell
dotnet run --project csharp/examples/create_msg_and_eml/create_msg_and_eml.csproj -- --msg-path example-message.msg --eml-path example-message.eml
```

### [msg_reader.cs](msg_reader.cs)

Use this example to:
- open a `.msg` through `MsgReader`
- inspect CFB geometry
- inspect top-level storages and streams
- list recipient and attachment storages
- dump the CFB tree

Run:

```powershell
dotnet run --project csharp/examples/msg_reader/msg_reader.csproj -- <path-to-msg>
```

### [msg_summary.cs](msg_summary.cs)

Use this example to:
- open a `.msg` through `MapiMessage`
- print high-level message metadata
- print transport headers
- print body preview
- list recipients and attachments
- inspect an arbitrary MAPI property

Run:

```powershell
dotnet run --project csharp/examples/msg_summary/msg_summary.csproj -- <path-to-msg>
```

## Recommended Starting Points

- If you want a complete end-to-end workflow, start with [create_msg_and_eml.cs](create_msg_and_eml.cs).
- If you want to inspect container internals, start with [msg_reader.cs](msg_reader.cs).
- If you want a high-level projection of an existing message, start with [msg_summary.cs](msg_summary.cs).
