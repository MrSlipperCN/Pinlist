# Pinlist

Pinlist is a quiet, always-on-top Windows task list with local SQLite storage and a Codex MCP integration.

## Current features

- Create and edit tasks with title, details, urgency, and an important flag.
- Complete tasks in place; completed tasks remain crossed out until the next local midnight.
- Drag tasks into a persistent manual order.
- Resize, collapse, move between monitors, hide during fullscreen apps, and restore from the tray.
- Restore completed or trashed tasks from **History & Trash** in the tray menu.
- Global quick-add shortcut: `Alt+Shift+P`.
- Undo recent completion, importance, and ordering changes with the on-screen action or `Ctrl+Z`.
- Single-instance behavior: launching Pinlist again activates the existing window.

## Development

```powershell
dotnet build src/Pinlist.Desktop/Pinlist.Desktop.csproj
dotnet run --project src/Pinlist.Desktop/Pinlist.Desktop.csproj
dotnet test tests/Pinlist.Tests/Pinlist.Tests.csproj
```

Local data is stored in `%LOCALAPPDATA%\Pinlist`.

## Install

Download the latest `Pinlist-win-Setup.exe` from
[GitHub Releases](https://github.com/MrSlipperCN/Pinlist/releases). The installer is per-user,
does not require administrator privileges, and includes the .NET runtime and the Pinlist MCP server.

Release builds check the public GitHub Releases feed in the background. When an update is available,
Pinlist downloads it and applies it the next time the app starts.

## Codex MCP

The stdio server is `src/Pinlist.Integration.Mcp`. It exposes tools for listing, creating, updating, completing, restoring, and trashing tasks. Both the desktop app and MCP server use the same local database.

Deploy a versioned MCP runtime with:

```powershell
.\scripts\Deploy-PinlistMcp.ps1
```

The versioned directory prevents an active Codex session from locking the next build. A new Codex session is required after deployment because tool manifests are loaded when the session starts.

For an installed release, register the bundled MCP server from a terminal with:

```powershell
codex mcp add pinlist -- "$env:LOCALAPPDATA\MrSlipperCN.Pinlist\current\mcp\Pinlist.Integration.Mcp.exe"
```

## Build a release

Create a local Windows x64 Velopack installer and update feed with:

```powershell
.\scripts\Build-Release.ps1 -Version 0.1.0
```

Pushing a tag such as `v0.1.0` runs `.github/workflows/release.yml`, builds the same artifacts,
and publishes them to GitHub Releases.
