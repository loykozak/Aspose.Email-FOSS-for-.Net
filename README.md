# Aspose.Email FOSS for .NET

Aspose.Email FOSS for .NET is a dependency-free C# library for working with:

- Compound File Binary (CFB) containers
- Outlook MSG files
- High-level mutable message objects
- EML parsing and serialization through an in-repository MIME implementation

The package is designed for deterministic binary and message processing without external runtime dependencies.

## Main Namespaces

- `Aspose.Email.Foss.Cfb`
- `Aspose.Email.Foss.Msg`

## Features

- Read and write CFB containers through `CfbReader` and `CfbWriter`
- Read and write MSG documents through `MsgReader`, `MsgWriter`, and `MsgDocument`
- Create, edit, save, and reload high-level messages through `MapiMessage`
- Load `.eml` into `MapiMessage` with `MapiMessage.LoadFromEml`
- Save `MapiMessage` back to `.eml` with `MapiMessage.SaveToEml`
- Work with recipients, regular attachments, and embedded message attachments

## Install

```powershell
dotnet add package Aspose.Email.Foss
```

## Quick Start

Read a subject from an MSG file:

```csharp
using System.IO;
using Aspose.Email.Foss.Msg;
using var stream = File.OpenRead("sample.msg");

var message = MapiMessage.FromStream(stream);
Console.WriteLine(message.Subject);
```

Create and save a message:

```csharp
using System.IO;
using Aspose.Email.Foss.Msg;

var message = MapiMessage.Create("Hello", "Body");
message.SenderName = "Alice";
message.SenderEmailAddress = "alice@example.com";
message.AddRecipient("bob@example.com", "Bob");
using var attachmentStream = new MemoryStream("abc"u8.ToArray());
message.AddAttachment("note.txt", attachmentStream, "text/plain");
using var output = File.Create("hello.msg");
message.Save(output);
```

Bridge between EML and MSG:

```csharp
using System.IO;
using Aspose.Email.Foss.Msg;
using var input = File.OpenRead("message.eml");

var message = MapiMessage.LoadFromEml(input);
using var msgOutput = File.Create("message.msg");
message.Save(msgOutput);
using var emlOutput = File.Create("roundtrip.eml");
message.SaveToEml(emlOutput);
```

## Build And Test

```powershell
dotnet build csharp/src/Aspose.Email.Foss/Aspose.Email.Foss.csproj -c Release
dotnet pack csharp/src/Aspose.Email.Foss/Aspose.Email.Foss.csproj -c Release
```

## Examples

Examples are available under `csharp/examples/`:

- `create_msg_and_eml.cs`
- `msg_reader.cs`
- `msg_summary.cs`
