# FolderFunBg: a learner's tour of a Windows Explorer extension

This tutorial is for college students and anyone learning Windows programming.
It walks through a real, working project — per-folder background images in
File Explorer — and explains the Windows concepts behind it. You do not need
to be an expert, but you should be comfortable reading C++, C#, and PowerShell.

> **Safety first.** FolderFunBg loads a DLL inside `explorer.exe` and hooks
> undocumented internals. Do this on a test VM or a machine you can restore.
> Never install two Explorer hooking tools at once. The full disclaimer is at
> the end.

---

## 1. What you are building

FolderFunBg has three parts:

| Part | Language | Runs where | Job |
|---|---|---|---|
| `FolderFunBg.Settings` | C# + WinUI 3 | Normal user app | Edit `config.json`, import images |
| `FolderFunBg.Core` | C# | Settings app and tests | Config model, validation, path rules |
| `FolderFunBg.Hook` | C++ | Inside `explorer.exe` | Detect folder views, draw the background |

The important idea: **the settings app never draws anything**. It only writes a
JSON file. The native hook watches that file and paints inside Explorer, so
backgrounds stay active after the settings app closes.

```
Settings app ──writes──▶ %LOCALAPPDATA%\FolderFunBg\config.json
                                      │
                              file watcher
                                      ▼
explorer.exe ── FolderFunBg.Hook.dll ── paints the folder view
```

---

## 2. How File Explorer is put together

Before hooking anything, learn what you are hooking.

### 2.1 HWNDs, one view at a time

A File Explorer window is a tree of window handles (`HWND`). A simplified view:

```
CabinetWClass                      the top-level Explorer window
└─ ShellTabWindowClass             a tab host
   └─ SHELLDLL_DefView             the shell view
      ├─ DirectUIHWND              the file list (icons, names, details)
      ├─ ...                       navigation pane, command bar, preview
```

Two class names matter most:

- `SHELLDLL_DefView` — the shell view that hosts a folder.
- `DirectUIHWND` — the window that renders modern Explorer UI using DirectUI,
  Microsoft's internal retained-mode UI toolkit (`dui70.dll`).

You can verify this yourself with **Process Explorer** (Sysinternals): select
`explorer.exe`, open the **Find** dialog, search for `DirectUIHWND`, and inspect
the parent chain. Microsoft Spy++ is another classic tool.

### 2.2 Why there is no official API

Windows exposes APIs for icons, thumbnails, and columns, but **not** for a
per-folder background image. The old `desktop.ini` `IconArea_Image` key is no
longer honored on current Windows 11 builds. That leaves unsupported options:

1. Hook a drawing function inside Explorer.
2. Wrap the shell view (`IShellView`) and draw yourself.
3. Use composition/DWM integration.

FolderFunBg uses option 1. It is version-sensitive by design: a Windows update
can change the internals tomorrow. A real product should plan for that.

---

## 3. How a Browser Helper Object loads

Explorer loads shell extensions through COM. `FolderFunBg.Hook.dll` is a
**Browser Helper Object** (BHO):

1. The DLL registers a COM class under
   `HKCR\CLSID\{80BDC533-D192-445A-A1D9-F91310021919}`.
2. It adds that CLSID under
   `HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects`.
3. Explorer calls `DllGetClassObject`, creates the object, and calls
   `SetSite` with the browser site.
4. The object subscribes to `DWebBrowserEvents2::DocumentComplete`, which tells
   it when a folder navigation finished.

Open `src/FolderFunBg.Hook/ExplorerBho.cpp` and follow `SetSite` →
`AppState::Start` → `Hooks::Install`. Notice the guard in `DllMain`: the DLL
only allows itself inside `explorer.exe` and `regsvr32.exe`, so it cannot be
accidentally loaded into unrelated processes.

> **Learning exercise:** use `regsvr32 /s FolderFunBg.Hook.dll` and then inspect
> the two registry keys with `regedit`. Unregister with `regsvr32 /u`.

---

## 4. Hooking with MinHook

[MinHook](https://github.com/TsudaKageyu/minhook) is a small x64 detour library.
It rewrites the first bytes of a target function with a jump to your function,
and gives you a trampoline to call the original.

FolderFunBg installs six hooks:

| Target | Why |
|---|---|
| `CreateWindowExW` | Detect new `DirectUIHWND` folder views |
| `DestroyWindow` | Forget closed windows |
| `BeginPaint` | Remember the HDC for a tracked window |
| `CreateCompatibleDC` | Remember double-buffer DCs |
| `FillRect` | GDI fallback background draw |
| `dui70!Element::PaintBackground` | Preferred DirectUI background draw |

The hook functions never let exceptions escape into Explorer. Every entry point
is wrapped in `try/catch`, logs failures, and returns the original result.

### 4.1 The DirectUI hook

The important addition is in `Hooks.cpp`:

```cpp
using PaintBackground_t =
    void(__fastcall*)(void*, HDC, void*, const RECT*,
                      const RECT*, const RECT*, const RECT*);

g_originalPaintBackground(self, hdc, value, rect, clip, exclude, target);
```

`PaintBackground` is a virtual method on `DirectUI::Element`. We resolve it by
its decorated export name:

```text
?PaintBackground@Element@DirectUI@@QEAAXPEAUHDC__@@PEAVValue@2@AEBUtagRECT@@222@Z
```

The hook calls the original first, then draws over the background but **under**
the item text and icons. That ordering is the whole trick: the DirectUI list
paints its own background first, then paints the file items. We insert our image
in between.

We only draw when the element is a folder view:

```cpp
const int role = g_getAccRole(self);      // MSAA role
if (role != ROLE_SYSTEM_LIST &&           // 33: the list itself
    role != ROLE_SYSTEM_LISTITEM)         // 34: a file row
{
    return;
}
```

`ROLE_SYSTEM_LIST` also fires for **empty** folders, which is why an empty
folder still gets a background. `GetRootRelativeBounds` gives the area to paint.

### 4.2 Why not only GDI `FillRect`?

The first implementation hooked `FillRect`. It works, but on Windows 11 build
26220 that GDI layer is composited **above** the item text: a large image covers
filenames. The DirectUI hook draws below them. FolderFunBg keeps `FillRect` as a
fallback for builds where the DirectUI export is missing or renamed.

This is a good lesson in engineering with undocumented APIs: **always ship a
fallback and log why it was used.**

---

## 5. Drawing an image with GDI+

Inside `DrawConfiguredBackground`:

1. Load the cached image (`ImageCache` validates PNG/JPEG signatures).
2. Compute the rectangle for the selected position:
   `top-left`, `top-right`, `bottom-left`, `bottom-right`, `center`, `stretch`,
   `zoom-fill`.
3. `SaveDC(hdc)`, clear the clip region, and clip to the view bounds.
4. `AlphaBlend` the cached GDI+ bitmap with the configured alpha.
5. Draw the readability scrim.
6. `RestoreDC(hdc, saved)`.

The scrim is a translucent rectangle or left-to-right gradient. `auto` mode
uses the image's average luminance and the Windows light/dark theme:

- Light theme + dark image → white scrim, so dark filenames stay readable.
- Dark theme + light image → black scrim.

`CachedImage::Luminance()` samples an 8×8 grid, so the decision is cheap.

> **Learning exercise:** add a `diagonal` position by computing `x` and `y`
> from the area size, then add it to `BackgroundPositions` and the settings
> ComboBox. This touches C++, C#, XAML, and tests — a complete vertical slice.

---

## 6. Config, rules, and hot reload

`config.json` lives in `%LOCALAPPDATA%\FolderFunBg`. A minimal document:

```json
{
  "version": 1,
  "imagesRoot": "%LOCALAPPDATA%\\FolderFunBg\\Images",
  "defaults": { "image": "sunset.png", "position": "bottom-right", "alpha": 255 },
  "readability": { "mode": "auto", "strength": 96, "gradient": true },
  "folders": [
    { "path": "D:\\Downloads", "image": "downloads.png", "recursive": false },
    { "path": "D:\\Projects", "image": "code.png", "alpha": 160 }
  ]
}
```

Resolution order (`PathRuleResolver`):

1. Normalize the folder path (expand environment variables, trim trailing `\`,
   compare case-insensitively).
2. Exact path rule wins; otherwise the longest matching recursive rule.
3. Shell namespace rules (`::{20D04FE0-...}`) match special folders.
4. Unspecified fields inherit `defaults`.

The hook runs a `ReadDirectoryChangesW` watcher (`AppState.cpp`). When the app
saves, the hook swaps in a new immutable config and invalidates tracked windows.
That is why backgrounds update without restarting Explorer.

> **Learning exercise:** add a "pause backgrounds" toggle to the app. Write
> `"enabled": false` in the config and make the hook skip drawing. This teaches
> you schema evolution and validation without touching the renderer.

---

## 7. Build and run from source

Prerequisites:

- Windows 10 1809+ / Windows 11, x64.
- Visual Studio 2022 with **Desktop development with C++** (MSVC v143).
- .NET SDK 9.0.102 (the repo pins it in `global.json`).

From the repository root:

```powershell
.\build.ps1
```

The script builds the native hook, the C# solution, runs the 43 core tests, and
stages files under `install\`.

Outputs:

```text
bin\x64\Release\FolderFunBg.Hook.dll
src\FolderFunBg.Settings\bin\x64\Release\...\FolderFunBg.Settings.exe
install\FolderFunBg.Hook.dll
```

Run the app and smoke-test it:

```powershell
.\src\FolderFunBg.Settings\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\FolderFunBg.Settings.exe
.\src\FolderFunBg.Settings\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\FolderFunBg.Settings.exe --smoke-test
```

The smoke test navigates all four pages and writes
`%TEMP%\FolderFunBg-smoke.txt`.

### Build the installer

```powershell
.\build-installer.ps1
```

This publishes the settings app self-contained, packs the hook plus app into a
payload, and produces:

```text
install\FolderFunBg-Setup.exe
```

The EXE requests elevation, stops/restarts Explorer, installs to
`%ProgramFiles%\FolderFunBg`, registers the BHO, and creates a Start Menu
shortcut. Run it with `--uninstall` to remove everything.

---

## 8. Debugging toolkit

When working on shell extensions, these tools save hours:

| Tool | Use it for |
|---|---|
| `%LOCALAPPDATA%\FolderFunBg\Logs\FolderFunBg.log` | Hook startup, navigation, draw decisions |
| Process Explorer | See if the DLL is loaded in `explorer.exe` |
| DebugView / DbgView | Capture `OutputDebugString` if you add it |
| WinDbg Preview | Break on `LoadLibrary`, inspect Explorer crashes |
| `regedit` | Verify the CLSID and BHO keys |
| `regsvr32 /u` | Unregister a broken build |

The hook log deliberately records only the first few draws (`DUI-DRAW`) so a
long-running Explorer does not fill the disk. When debugging, add a bounded
counter around a new log line rather than logging every paint.

Common failure modes:

1. **No background at all** — check the log for `PaintBackground hook installed`;
   otherwise the DirectUI export was not found and the GDI fallback should run.
2. **Background above filenames** — the fallback path is active, or another hook
   tool is installed. Uninstall the other tool.
3. **Hook not loaded** — `regsvr32` failed, the DLL is not in Program Files, or
   Explorer was not restarted.
4. **Black image / unreadable text** — the configured image is very dark and
   readability is `none`. Use `auto`.

> **Learning exercise:** add a `--diagnostics` page to the settings app that
> prints whether the BHO is registered, the DLL path, and the last 20 log lines.

---

## 9. More exercises

### Beginner

- Change the default alpha and observe the difference.
- Add a new image position and wire it through Core, the app, and tests.
- Write a unit test for a recursive rule that is longer than an exact rule.

### Intermediate

- Add an "enabled" flag to folder rules.
- Show a live thumbnail of the selected image in the Folders page.
- Add per-monitor DPI awareness tests; make sure the image does not blur.

### Advanced

- Add a hotkey that cycles backgrounds.
- Cache decoded images with a memory budget and evict least-recently used.
- Investigate an `IShellView` wrapper as a more supported alternative to
  hooking DirectUI. Compare stability and complexity in a short design doc.

---

## 10. Packaging and GitHub releases

`build-installer.ps1` produces a self-contained `FolderFunBg-Setup.exe`. The
project publishes it as a GitHub Release asset rather than committing binaries:

```powershell
gh release create v0.1.0 install\FolderFunBg-Setup.exe `
  --title "FolderFunBg v0.1.0" `
  --notes "First preview release. Installs the Explorer hook and settings app."
```

The installer source is in `installer/FolderFunBg.Installer`. It embeds a
`payload.zip` generated at build time. The ZIP contains:

```text
Hook/FolderFunBg.Hook.dll
Settings/...                 self-contained WinUI app
```

The installer is a normal .NET console app with a `requireAdministrator`
manifest. It extracts the payload, stops Explorer so the old DLL is unlocked,
copies the files, calls `regsvr32`, creates a shortcut, and restarts Explorer.

> **Learning exercise:** add a `/verify` switch that extracts the payload,
> checks the expected files exist, and exits without modifying the machine.

---

## 11. Limits, ethics, and safety

- This is an **unsupported** extension point. Windows updates can break it.
- Only run code you built or trust. A BHO runs with your user's privileges
  inside Explorer and can read your files.
- Do not use hooking to hide, tamper with, or bypass security controls.
- Do not impersonate Microsoft branding. FolderFunBg is a third-party tool.
- Keep a restore point or VM snapshot before installing shell extensions.
- Never install FolderFunBg alongside another tool that hooks the same
  drawing path.

The project is MIT licensed. MinHook and `nlohmann/json` keep their own licenses;
see `LICENSE` and `NOTICE.md`.

---

## Glossary

| Term | Meaning |
|---|---|
| **BHO** | Browser Helper Object: a COM DLL loaded by Explorer |
| **CLSID** | Globally unique COM class identifier |
| **DC / HDC** | Device context: the GDI drawing surface |
| **DirectUI** | Microsoft's internal UI toolkit in `dui70.dll` |
| **GDI+** | The .NET-era 2D drawing API used here for images |
| **HINSTANCE** | Handle to a loaded module |
| **HWND** | Handle to a window |
| **MinHook** | A small library that detours functions at runtime |
| **MSAA role** | Accessibility role such as `ROLE_SYSTEM_LIST` (33) |
| **Scrim** | A translucent layer that improves text contrast |
| **Trampoline** | Saved original bytes used to call a hooked function |

## Further reading

- Microsoft Learn: COM, `IObjectWithSite`, `DWebBrowserEvents2`
- Microsoft Learn: GDI+, `AlphaBlend`, device contexts
- MinHook documentation and source
- `docs/architecture.md` and `docs/compatibility.md` in this repository
- The MIT-licensed `Maplespe/explorerTool` reference technique

Build something small, log everything, and keep a snapshot you can roll back to.
