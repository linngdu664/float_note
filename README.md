# FloatNote / 浮签

FloatNote is a lightweight Windows floating note and todo app built with WPF.

## Current MVP

- Always-on-top floating note window
- Auto-saved note text
- Todo title and optional content
- Todo collapse and expand
- Todo add, complete, hide completed, delete
- Current todo marking with double click
- Floating ball launcher with edge docking
- Hover preview for current todos
- Light and dark theme switching
- Window position and size persistence
- System tray show, hide, exit
- Global hotkey: `Ctrl + Alt + N`
- Local JSON storage

## Run

```powershell
dotnet run
```

## Build

```powershell
dotnet build
```

## Publish exe

Framework-dependent:

```powershell
dotnet publish -c Release -r win-x64
```

Self-contained:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## Data

Application data is stored at:

```text
%LOCALAPPDATA%\FloatNote\app-state.json
```
