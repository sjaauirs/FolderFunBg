# FolderFunBg

Per-folder background images for Windows Explorer, with a modern WinUI 3
settings app. A small native extension loaded inside `explorer.exe` paints the
background; the settings app only writes configuration, so it does not need to
stay running.

> Status: early preview. The native hook, config core, WinUI app, unit tests,
> page smoke test, and on-machine install have been verified on Windows 11 x64.
> The previous `ExplorerBgTool` BHO has been unregistered. On this Windows build,
> Explorer composites the hook's image above the file list, so use corner
> positions and/or lower alpha when the image overlaps file names.

## Features

- Per-folder background image with optional position, alpha, random, recursive,
  and readability overrides.
- Browse folders and import images from the WinUI settings app.
- Readability scrim: auto, light, dark, or none, with adjustable strength and a
  left-to-right gradient. Auto uses the image luminance and the Windows
  light/dark theme. On this Windows build the image is composited above the
  file list; use a corner position or lower alpha when it overlaps names.
- Tray-less and settings-only. Explorer loads the extension automatically;
  closing the settings app leaves backgrounds active.
- Per-user configuration and images under `%LOCALAPPDATA%\FolderFunBg`.
- No visible third-party branding. The DLL metadata, CLSID, registry keys, and
  UI are FolderFunBg. Third-party license texts live in `LICENSE`/`NOTICE`.

## Architecture

- `src/FolderFunBg.Hook` — native x64 COM Browser Helper Object. It detects
  Explorer's `DirectUIHWND` folder view, hooks the background erase path with
  MinHook, and draws the image plus readability scrim with GDI+.
- `src/FolderFunBg.Core` — config model, path/CLSID rule resolution,
  validation, atomic writes, image library, and legacy `config.ini` migration.
- `src/FolderFunBg.Settings` — WinUI 3 settings app.
- `tests/FolderFunBg.Core.Tests` — xUnit tests for the core.
- `installer` — elevated install/uninstall scripts.

See `docs/architecture.md` for the hook mechanics and `docs/compatibility.md`
for the support matrix.

## Prerequisites

- Windows 10 1809+ or Windows 11, x64.
- Visual Studio 2022 with **Desktop development with C++** (MSVC v143, Windows
  SDK 10.0.22621) for the native hook.
- .NET SDK 9.0.102 (the repository `global.json` pin; `latestFeature` allows a
  newer 9.0.x patch/feature band).
- Network access on first build to restore `Microsoft.WindowsAppSDK`.

## Build

From this directory:

```powershell
.\build.ps1
```

The script locates MSBuild and the C++ toolchain with `vswhere`, builds the
native hook and the C# solution, runs the core tests, and stages output under
`install\`.

Outputs:

- `bin\x64\Release\FolderFunBg.Hook.dll`
- `src\FolderFunBg.Settings\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\FolderFunBg.Settings.exe`
- `install\FolderFunBg.Hook.dll` and the install/uninstall scripts

For a faster native-only build: `build-native.cmd`.

## Run the settings app

```powershell
.\src\FolderFunBg.Settings\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\FolderFunBg.Settings.exe
```

Pages:

- **Folders** — add a folder, pick/import an image, set position/alpha,
  recursive, enabled, and a readability override, then Apply.
- **Image library** — import multiple images, preview, and delete.
- **Defaults** — default image/style and the global readability scrim, with a
  live preview.
- **Settings** — extension status, install/uninstall, open config/image/log
  folders, and import a legacy `ExplorerBgTool` `config.ini`.

The app also has a diagnostic mode:

```powershell
FolderFunBg.Settings.exe --smoke-test
```

It navigates every page, writes `%TEMP%\FolderFunBg-smoke.txt`, and exits with a
non-zero code if a page fails to load.

## Install

> **Do not run FolderFunBg and ExplorerBgTool at the same time.** Both hook
> `FillRect`/`DirectUIHWND`; two hook stacks can crash Explorer.

1. Unregister the existing `ExplorerBgTool` BHO first (run its `Uninstall.cmd`
   as administrator, or `regsvr32 /u` its DLL). The installer also detects the
   legacy CLSID and refuses unless `-Force` is passed.
2. Run `installer\install.ps1` from an elevated PowerShell, or use
   **Settings → Install extension** in the app.
3. Explorer is restarted by the installer so the BHO loads.

```powershell
.\installer\install.ps1 -DllPath ".\bin\x64\Release\FolderFunBg.Hook.dll" -RestartExplorer
```

Uninstall:

```powershell
.\installer\uninstall.ps1 -RestartExplorer
```

The install script copies the DLL to `%ProgramFiles%\FolderFunBg`, calls
`regsvr32`, and registers the CLSID/BHO. The uninstall script reverses it.
Per-user config/images are left in place.

## Configuration

- Config: `%LOCALAPPDATA%\FolderFunBg\config.json`
- Images: `%LOCALAPPDATA%\FolderFunBg\Images`
- Logs: `%LOCALAPPDATA%\FolderFunBg\Logs\FolderFunBg.log`

The settings app owns the file and writes it atomically. The hook watches the
directory and reloads without a separate process. A sample is in
`samples/config.example.json`:

```json
{
  "version": 1,
  "imagesRoot": "%LOCALAPPDATA%\\FolderFunBg\\Images",
  "defaults": {
    "image": "default.png",
    "position": "bottom-right",
    "alpha": 255,
    "random": false
  },
  "readability": {
    "mode": "auto",
    "strength": 96,
    "gradient": true
  },
  "folders": [
    {
      "path": "C:\\Users\\pc\\Downloads",
      "image": "downloads.png",
      "position": "bottom-right",
      "alpha": 255
    },
    {
      "path": "C:\\Users\\pc\\Documents",
      "image": "documents.jpg",
      "readability": { "mode": "light", "strength": 140 }
    },
    {
      "clsid": "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
      "image": "thispc.png"
    }
  ]
}
```

- `position`: `top-left`, `top-right`, `bottom-left`, `bottom-right`, `center`,
  `stretch`, `zoom-fill`.
- `alpha` and `readability.strength`: 0–255.
- `image` is a file name inside the image library, never a full path.
- `path` rules match case-insensitively; `recursive` applies to subfolders.
- CLSID rules use shell namespace IDs such as `::{20D04FE0-...}` (This PC).
- A rule's `readability` object is a partial override; omitted fields inherit
  the global readability settings.

## Readability

Explorer draws filenames and icons after the background erase. FolderFunBg
draws a scrim in that same pass, between the image and the items:

- `auto` — light theme + dark image → white scrim; dark theme + light image →
  black scrim. Strength scales with how far the image is from the readable
  range.
- `light` / `dark` — force a white or black scrim.
- `none` — no scrim.
- `gradient` — strongest over the left file-name column, fading to transparent.

The scrim mutes the image and improves its contrast, but text underneath an
opaque image is still covered on this Windows build. Use a corner position or
lower `alpha` when the image overlaps the file list. The Defaults page preview
approximates auto with the current theme.

## Rendering limitation on Windows 11

The current hook draws in Explorer's GDI background layer. On Windows 11 build
26220 that layer is composited **above** the item text, so an image can cover
file names where it overlaps. The previous `ExplorerBgTool` uses the same
`FillRect` technique; its bottom-right default makes the limitation less
visible. FolderFunBg defaults to bottom-right and warns in the UI.

Practical mitigations: corner positions, smaller images, or `alpha` below 255.
A true behind-the-icons background needs a shell view extension (`IShellView`
wrapper) or composition-level integration; that is future work. The legacy
`desktop.ini` `IconArea_Image` feature was tested and is no longer honored on
this Windows build.

## Safety and compatibility

- Windows has no supported per-folder background API; this is a
  version-sensitive in-Explorer hook. Explorer updates can break it.
- The hook fails closed: invalid config or images produce no background.
- A corrupt image can still destabilize GDI+; import images through the app so
  they are signature-validated, and keep `noerror`-style fallback behavior.
- Sign release binaries. An unsigned BHO using MinHook may trigger SmartScreen
  or antivirus heuristics.
- Do not impersonate Microsoft/Windows branding. FolderFunBg is a third-party
  tool that aims to feel native, not an official Windows component.

## Tests and CI

```powershell
dotnet test tests\FolderFunBg.Core.Tests\FolderFunBg.Core.Tests.csproj
```

`.github/workflows/build.yml` runs `build.ps1` on `windows-latest` for pushes
and pull requests.

## License

MIT. See `LICENSE` and `NOTICE.md` for MinHook, nlohmann/json, and the
technique reference.
