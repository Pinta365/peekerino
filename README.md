# Peekerino

Peekerino is a Windows tray utility that lets you press `Ctrl+Alt+Space` in File Explorer to peek at the selected item. It shows metadata, previews text files, streams CSV rows with quick stats, summarizes XML-files, pretty-prints JSON, lists ZIP contents, and analyzes binaries without opening heavyweight tools.

## Quick Start

1. Clone the repository.
2. Install the .NET SDK 9.0 preview or newer.
3. From the repo root run `dotnet run --project peekerino.csproj` (or `dotnet build`).
4. With the app running, press `Ctrl+Alt+Space` while a file or folder is selected in Explorer to open the preview window.

## Configuration

Peekerino loads options from `appsettings.json` (copied alongside the executable). You can tweak without recompiling:

```json
{
  "Peekerino": {
    "Hotkey": {
      "Modifier": 3,
      "Key": 32
    },
    "Summary": {
      "TextPreviewBytes": 4096,
      "JsonMaxCharacters": 40000,
      "ArchiveMaxEntries": 100,
      "ArchivePreviewBytes": 512,
      "BinaryHeaderBytes": 256,
      "BinaryEntropySampleBytes": 65536,
      "BinaryStringScanBytes": 4096,
      "BinaryStringSampleCount": 5
    }
  }
}
```

- `Modifier` uses the Win32 `MOD_*` bit flags (3 = Ctrl+Alt); `Key` is a virtual-key code (32 = Space).
- Adjust any summary limits as needed before launching the app.

## Publishing

Use the included PowerShell script to generate a distributable build:

```powershell
# Default: Release, win-x64, self-contained single file
.\publish.ps1

# Alternate runtime/config combos
.\publish.ps1 -Runtime win-arm64
.\publish.ps1 -SelfContained:$false -SingleFile:$false

# Build, create git tag, push it (push triggers the GitHub release workflow)
.\publish.ps1 -Tag v0.6.0 -PushTag
```

Packages land in `bin/<Configuration>/net9.0-windows/<Runtime>/publish/`.

## Project Layout

```
├── Configuration/
│   └── PeekerinoOptions.cs        # Strongly-typed options bound from appsettings.json
├── Services/
│   ├── ArchiveSummaryService.cs   # Zip/Tar/Tgz/GZip table-of-contents + preview pipeline
│   ├── BinarySummaryService.cs    # Signature, SHA256, entropy, printable strings
│   ├── CsvSummarizer.cs           # Column listing, numeric stats, preview rows
│   ├── FileSummaryResult.cs       # Summary DTO with tables + text preview
│   ├── FileSummaryService.cs      # Orchestrates IFileSummarizer plugins
│   ├── IncaDocumentSummarizer.cs  # Specialized INCA insurance XML summary
│   ├── JsonSummarizer.cs          # Pretty-printed JSON output
│   ├── Summaries/                 # Pluggable summarizers + helpers
│   │   ├── FileSummaryContext.cs  # Info passed to each summarizer
│   │   ├── FileTypeInspector.cs   # Heuristics for XML/JSON/archive/text detection
│   │   ├── IFileSummarizer.cs     # Interface for summary plugins
│   │   ├── ArchiveFileSummarizer.cs, CsvFileSummarizer.cs, ...
│   └── TextTableBuilder.cs        # ASCII table formatter used by archive/csv summaries
├── Shell/
│   └── ExplorerSelectionProvider.cs # COM interop to fetch Explorer selection
├── UI/
│   ├── MainForm.cs                # Hidden tray host, global hotkey, DI bootstrap
│   ├── PreviewForm.cs             # Resizable preview window with tables + text
│   └── TrayIconController.cs      # NotifyIcon and tray menu wiring
├── appsettings.json               # Hotkey and summary limits (copied to output)
├── Program.cs                     # Entry point: configuration + DI setup
├── publish.ps1                    # Helper script for dotnet publish
└── README.md                      # You are here
```

Feel free to add more `IFileSummarizer` implementations (e.g., Markdown, Office docs); just register them in `Program.cs` and they’ll slot into the pipeline.

## Contributing

- Follow the formatting rules in `.editorconfig` and run `dotnet format` before sending a pull request.

## License

MIT License. See `LICENSE` for details.

