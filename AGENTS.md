# Agent Instructions

Guidelines for AI agents working on the **Peekerino** Windows tray project.

## Project Stack

- **Runtime**: .NET 9 (C#)
- **Desktop Framework**: Windows Forms
- **Architecture**: DI container (Microsoft.Extensions), pluggable `IFileSummarizer` pipeline
- **Configuration**: `appsettings.json` (copied to output)

## Build & Run

- **DO NOT** modify or run long-lived GUI processes unless the user asks for it.
- Restore/build: `dotnet build`
- Run the tray app: `dotnet run --project peekerino.csproj`
- Package for distribution: `.\publish.ps1` (defaults to win-x64, Release, self-contained single file)

## Code Style

- Minimize inline comments inside method bodies; rely on clear names.
- XML documentation comments are welcome for public APIs or tricky logic.
- Prefer structured summaries (`FileSummaryResult`, `TableSummary`, `TextPreview`) over raw strings.
- Keep hotkey/config defaults in `PeekerinoOptions` and `appsettings.json`—no magic numbers in code.

## Project Structure

```
Configuration/PeekerinoOptions.cs   # Strongly-typed options
Services/                           # Summaries, helpers, DI pipeline
  Summaries/                        # Individual IFileSummarizer implementations
Shell/ExplorerSelectionProvider.cs  # Explorer COM interop
UI/                                 # WinForms UI (MainForm, PreviewForm, Tray controller)
Program.cs                          # DI + configuration bootstrap
publish.ps1                         # Packaging helper
```

## Development Guidelines

- Read relevant files before editing; respect the summarizer pipeline and DI setup.
- Keep the preview window responsive; avoid blocking `PreviewForm_Load` with heavy sync work.
- Add new file-type support by implementing `IFileSummarizer` and registering it in `Program.cs`.
- Prefer configuration-driven behaviour (hotkeys, preview limits) over hard-coded values.
- Maintain minimal dependencies—no heavy frameworks without user approval.
- Preserve existing functionality; if behaviour must change, call it out explicitly.

## Common Tasks

- Restore & build: `dotnet build`
- Lint/style: rely on compiler + code review (no extra tooling baked in)
- Update README when introducing new workflows or file types
- Publish portable build: `.\publish.ps1`

When in doubt, ask for clarification before making broad changes. Keep diffs focused and easy to review.

