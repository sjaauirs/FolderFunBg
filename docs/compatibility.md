# Compatibility and testing

## Build environment

| Component | Verified here |
|---|---|
| Windows | Windows 11 Pro 10.0.26220, x64 |
| Visual Studio | 2022 Community 17.12.4, MSVC 14.42, Windows SDK 10.0.22621 |
| .NET SDK | 9.0.102 |
| Windows App SDK | 2.5.1 (NuGet restore) |
| Native hook | x64, v143, static CRT, MinHook vendored |

## Runtime support

- Windows 10 1809+ and Windows 11, x64.
- Explorer's classic folder view (`SHELLDLL_DefView` + `DirectUIHWND`). Explorer
  tabs are expected to work because each tab creates a `DirectUIHWND` under the
  shell tab window; test across tab transitions.
- File-dialog backgrounds are **not enabled** in this preview.
- The settings app requires the .NET 9 runtime (or a self-contained publish).

## Known limitations

- There is no supported Windows API for per-folder backgrounds. This is a
  version-sensitive in-Explorer hook; a Windows/Explorer update can break it.
- On Windows 11 the DirectUI `PaintBackground` hook renders the image beneath
  file icons and names. If that hook cannot be installed (for example after an
  Explorer update renames the exported method), the GDI `FillRect` fallback is
  composited above the item text on build 26220; in fallback mode use corner
  positions, smaller images, or lower `alpha`. The legacy `desktop.ini`
  `IconArea_Image` feature does not work on this build.
- A malformed image can destabilize GDI+ and Explorer. Import images through
  the app so signatures are validated, and avoid replacing files underneath a
  running Explorer.
- The BHO is machine-wide and needs administrator rights once. Config and
  images are per-user, so daily use does not need elevation.
- Unsigned BHOs using MinHook can trigger SmartScreen/antivirus heuristics.
  Sign release builds.
- Do not install alongside `ExplorerBgTool`; both hook the same rendering path.

## Manual test matrix

- Windows 10 and Windows 11, fresh Explorer window.
- Navigation between two folders with different rules.
- Exact vs recursive rules; CLSID rule (This PC).
- Position modes: top-left, top-right, bottom-left, bottom-right, center,
  stretch, zoom-fill.
- Alpha 0/128/255.
- Readability auto/light/dark/none; gradient on/off; a very dark image in light
  theme and a very light image in dark theme.
- High DPI (100%/150%/200%), multi-monitor, maximize/restore/resize.
- Explorer tabs and multiple Explorer windows.
- Missing image, corrupt image, invalid JSON, config reload while open.
- Explorer restart, install, uninstall, non-admin config editing.
- Smoke test: `FolderFunBg.Settings.exe --smoke-test` exits 0 and writes
  `%TEMP%\FolderFunBg-smoke.txt` with four PASS lines.
