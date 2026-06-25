# CuteSpace

CuteSpace is a small floating Windows companion for organizing modes, shortcuts, routines, and recent clipboard items from one soft, always-available bubble.

It is built with C#, .NET 8, and WinUI 3. The app runs locally on your PC and stores its data in your Windows user profile.

## Features

- Floating desktop bubble with a compact panel.
- Modes for grouping apps, files, folders, websites, and Windows tools.
- Quick shortcuts with custom icons.
- Optional startup mode when Windows opens.
- Clipboard history for text, links, and images.
- Focus timer and counter.
- Local settings, local logs, and multilingual UI.

## Screenshots

Screenshots can be added in `docs/screenshots/`.

## Requirements

- Windows 10 version 1809 or newer.
- Windows x64.
- For development: .NET 8 SDK and Visual Studio 2022 with WinUI/Windows App SDK support.

The release build is self-contained and includes the Windows App SDK runtime files needed by the app.

## Install

1. Download the Windows `.zip` release.
2. Extract it to any folder.
3. Run `CuteSpace.exe`.

Windows may show a security warning because this first public version is not code-signed.

## Run From Source

```powershell
dotnet restore .\CuteSpace.sln
dotnet build .\CuteSpace.sln -c Release -p:Platform=x64
dotnet run --project .\src\CuteSpace\CuteSpace.csproj -c Release -p:Platform=x64
```

You can also open `CuteSpace.sln` in Visual Studio 2022, select `x64`, and run the `CuteSpace` project.

## Build For Windows

```powershell
.\scripts\build-windows.ps1
```

Upload `CuteSpace-v0.1.0-windows-x64.zip` to itch.io as the Windows build.

For manual builds, use Visual Studio Developer PowerShell and publish the `CuteSpace` project in `Release|x64`.

## Data And Privacy

CuteSpace works locally. It does not upload your modes, shortcuts, clipboard history, settings, or logs. See `PRIVACY.md` for details.

## Project Structure

```text
CuteSpace.sln
src/
  CuteSpace/
    Assets/
    Models/
    Resources/Languages/
    Services/
docs/
  screenshots/
release/
  windows/
```

## Donations

If CuteSpace helps you, you can support development on Ko-fi:

https://ko-fi.com/tu_usuario

Replace the link above with your Ko-fi page before publishing.

## License

MIT. See `LICENSE`.
