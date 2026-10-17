# FolderFunBg architecture

## Components

```
FolderFunBg.Settings (C# WinUI 3)
  ├─ Folder rules / image library / defaults / readability
  └─ writes %LOCALAPPDATA%\FolderFunBg\config.json atomically
                 │
                 │ file watcher (ReadDirectoryChangesW)
                 ▼
explorer.exe
  └─ FolderFunBg.Hook.dll (native x64 COM BHO)
       ├─ IObjectWithSite + DWebBrowserEvents2 (DOCUMENTCOMPLETE)
       ├─ MinHook: CreateWindowExW, DestroyWindow, BeginPaint, FillRect, CreateCompatibleDC
       ├─ detects DirectUIHWND under SHELLDLL_DefView
       ├─ GDI+ image cache + AlphaBlend
       └─ readability scrim (GDI+ gradient/solid)
```

There is no Windows service and no persistent background process. The BHO is
loaded by Explorer; the settings app is only a configuration editor.

## Registration

`DllRegisterServer` writes:

- `HKCR\CLSID\{80BDC533-D192-445A-A1D9-F91310021919}\InProcServer32`
  - default = DLL path
  - `ThreadingModel` = `Apartment`
- `HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{80BDC533-...}`
  - `NoInternetExplorer` = 1

`DllUnregisterServer` removes them. The installer copies the DLL to
`%ProgramFiles%\FolderFunBg` and calls `regsvr32`; it requires administrator
rights and restarts Explorer.

The CLSID is intentionally different from the previous `ExplorerBgTool`
(`{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}`) so the installer can detect and
refuse a conflicting installation. `ShouldLoad` only returns true in
`explorer.exe` and `regsvr32.exe`; file-dialog hosting is not enabled in this
preview.

## Hook flow

1. Explorer creates the BHO object. `CObjectWithSite` starts `AppState`:
   GDI+, config load, the config watcher, and MinHook.
2. `CreateWindowExW` is hooked. When a `DirectUIHWND` is created whose parent is
   `SHELLDLL_DefView` and grandparent is `ShellTabWindowClass` or `#32770`, the
   HWND is tracked per Explorer thread.
3. `BeginPaint` records the HDC for the tracked HWND. `CreateCompatibleDC`
   associates a memory DC with the tracked window.
4. `FillRect` calls the original background erase, then:
   - finds the tracked window for the HDC/thread,
   - resolves the current folder path to a rule (`DOCUMENTCOMPLETE` supplies
     the path),
   - computes the position/size for the configured mode,
   - clips to the dirty rect,
   - `AlphaBlend`s the cached image,
   - draws the readability scrim,
   - restores the DC.
5. `DestroyWindow` removes tracking. Config changes trigger a reload and
   `InvalidateRect` on tracked windows.

The hook never throws across an Explorer callback; failures are logged and the
background is skipped.

### Rendering limitation on Windows 11 26220

The `FillRect` GDI layer is composited **above** the DirectUI item text on this
build. An opaque image therefore covers file names where it overlaps. The old
`ExplorerBgTool` uses the same technique; a bottom-right placement makes the
overlap less noticeable. Mitigations in FolderFunBg are corner positions,
smaller images, and lower `alpha`. A true behind-the-icons background requires
a shell view wrapper (`IShellView`) or composition-level integration; the
legacy `desktop.ini` `IconArea_Image` was tested and is no longer honored on
this build.

## Config and rule resolution

- Config and images are per-user (`%LOCALAPPDATA%\FolderFunBg`), resolved inside
  the Explorer process via `SHGetKnownFolderPath`.
- Paths are expanded, normalized, case-insensitive, and matched exactly first;
  the longest matching recursive rule wins.
- CLSID rules are normalized to `::{GUID}` and matched exactly.
- Unspecified style fields inherit `defaults`; a rule's `readability` object is
  a partial override of the global readability settings.
- Image names are validated file names inside the image library; absolute paths
  and traversal are rejected. The image cache validates PNG/JPEG signatures and
  samples 8×8 pixels for average luminance before drawing.

## Readability

`DrawReadabilityScrim` is called after the image and before Explorer paints
items:

- `auto`: in light theme, a dark image gets a white scrim; in dark theme, a
  light image gets a black scrim. Strength scales with distance from the
  readable luminance range (0.45/0.55).
- `light`/`dark`: force the scrim color.
- `none`: no scrim.
- `gradient`: full strength at the left edge, transparent at the right, so the
  file-name column is protected while the image remains visible.

The settings preview approximates auto with the current theme; the in-Explorer
behavior uses the image luminance.

## Threading and lifecycle

- Explorer's UI threads call the hooks; tracking maps are guarded by a mutex.
- The config watcher runs on its own thread and swaps an immutable `Config`
  shared pointer under a shared mutex.
- `CachedImage::Draw` serializes drawing per image; the image cache is
  mutex-protected.
- `DllMain` only allows the DLL in Explorer/regsvr32. Dynamic unload stops the
  watcher, removes hooks, and shuts down GDI+; process termination skips the
  heavier teardown.

## Why not a Windows service?

Windows services run in Session 0 and cannot paint in the interactive
Explorer desktop. A service could manage config, images, updates, or launch a
per-user helper, but the rendering must happen inside Explorer (this BHO) or in
a per-user overlay process. FolderFunBg deliberately uses the in-Explorer BHO
and no service.

## Differences from the reference technique

The reference project (`Maplespe/explorerTool`, MIT) established the
BHO + MinHook + `FillRect` approach. FolderFunBg is a clean implementation with:

- JSON config and a WinUI 3 editor instead of an INI file.
- Per-folder position/alpha/random and readability overrides.
- Luminance/theme-aware readability scrim.
- Atomic config writes and a file watcher.
- Per-user config resolution inside Explorer.
- No third-party branding; MinHook and nlohmann/json retain their own licenses.
