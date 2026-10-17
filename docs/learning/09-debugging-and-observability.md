# Chapter 9 — Debugging and observability

**Learning goals**

- Design logs that are useful and cheap enough for a render path.
- Use Process Explorer, Event Viewer, WER, and a debugger to diagnose a shell
  extension.
- Recognize the common failure signatures in this project.
- Collect evidence before changing code.

**Prerequisites:** Chapters 3–5. A VM or a machine you can restart.

---

## 9.1 Observability in a render path

You cannot attach a debugger to Explorer every time something looks wrong. The
hook needs lightweight evidence:

- `%LOCALAPPDATA%\FolderFunBg\Logs\FolderFunBg.log`
- `%ProgramData%\FolderFunBg\setup.log` (installer)
- `%TEMP%\FolderFunBg-smoke.txt` (settings app)
- Windows Event Viewer / WER for crashes

Logging rules used here:

1. **Bounded events.** `DUI-DRAW` logs only the first 20 draws.
2. **No payload dumps.** Log paths and decisions, not image bytes.
3. **No per-paint logs.** A paint-time log line can cause thousands of writes
   per second.
4. **One line per event.** Keep it parseable and timestamped at the source.
5. **Never throw from logging.** A failed log must not break the feature.

`Logging.cpp` uses a mutex and appends to a file. It is simple and adequate for
a preview. For a production tool, consider ETW (Event Tracing for Windows):
lower overhead, structured events, and standard tooling.

## 9.2 The tools

| Tool | What it answers |
|---|---|
| **Process Explorer** | Is the DLL loaded? Which process/threads? What handles are open? |
| **DebugView** | Captures `OutputDebugString` from debug builds |
| **Event Viewer** | Application Error / WER entries for crashes |
| **WinDbg Preview** | Stacks, exceptions, `!analyze -v`, breakpoints |
| **Process Monitor** | File, registry, and process events (useful for installers) |
| **Accessibility Insights** | UI automation names, keyboard, contrast |
| **dotnet-dump / dotnet-trace** | Managed app crashes and CPU traces |

### Process Explorer checklist

1. Select `explorer.exe`.
2. `Ctrl+F` → search for `FolderFunBg.Hook.dll`.
3. If found, the BHO is loaded.
4. Open the **Threads** tab to see which threads run in the DLL.
5. Use **Find Handle** to check whether the config/log files are open.

### Event Viewer checklist

1. Open `eventvwr.msc`.
2. **Windows Logs → Application**.
3. Look for `Application Error` and `Windows Error Reporting` around the crash
   time.
4. Note the faulting module and exception code. For `0xC000027B`, the stowed
   exception details are often in the WER report folder.

## 9.3 Failure signatures

| Signature | Evidence | Likely cause |
|---|---|---|
| No `DUI background: PaintBackground hook installed` | Hook log | `dui70` export missing/renamed, or BHO not loaded |
| `PaintBackground hook installed` but no `DUI-DRAW` | Hook log | Wrong role/bounds, empty config, or no image |
| `DUI-DRAW role=34` only | Hook log | An older build without the `ROLE_SYSTEM_LIST` trigger; empty folders will not draw |
| Background above file names | Visual | GDI fallback is active, or another hook tool is installed |
| `config load failed` | Hook log | Invalid JSON or validation failure; previous config stays active |
| Explorer crashes when opening a folder | Event Viewer/WER | Hook signature mismatch, corrupt image, or a second hook stack |
| Installer copy fails | `setup.log` | Explorer still holds the DLL; the stop/retry logic did not win the race |
| Installed app crashes immediately (0xC000027B) | Event Viewer | Missing `FolderFunBg.Settings.pri` or other publish assets |

## 9.4 Debugging a native shell extension

### Prepare a VM

Do not debug Explorer on your daily machine unless you accept that you may lose
the desktop. Use a VM snapshot. If you must use a real machine, create a restore
point and keep an uninstall path on a USB stick.

### Attach vs. launch

- **Attach** to a running `explorer.exe`: good for inspecting the loaded DLL and
  current state. Symbols for Explorer are partly available from Microsoft's
  symbol server; `dui70.dll` symbols may not be.
- **Launch** Explorer under the debugger: `windbg -o explorer.exe`. This can be
  awkward because Explorer is normally already running.
- **Debug the installer or settings app** normally; they are ordinary desktop
  processes.

### Analyzing a crash

1. Collect the WER minidump from
   `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\...`.
2. Open it in WinDbg Preview.
3. Run:

```text
.symfix
.reload
!analyze -v
```

4. Look at the faulting module, exception code, and the call stack.
5. If the crash is in `Microsoft.UI.Xaml.dll` at startup, suspect the app
   package/PRI/resources first.
6. If the crash is in `FolderFunBg.Hook.dll`, check the hook signature and
   whether the target function's prologue is safe to detour.

### Native symbols

The build produces a PDB next to the hook DLL in `bin\x64\Release`. Keep it for
debugging. Do not ship PDBs in the installer unless you have a symbol server or
want users to see them; the build script strips them from the payload.

## 9.5 Debugging workflow

Before changing code:

1. **Reproduce** with the smallest folder and the simplest config.
2. **Collect**: hook log, installer log, Event Viewer entry, screenshot.
3. **Localize**: is the problem in config parsing, hook installation, folder
   resolution, or painting?
4. **Test one hypothesis at a time.**
5. **Revert instrumentation** when done.

A useful pattern is to add a temporary environment-variable switch:

```cpp
if (GetEnvironmentVariableW(L"FOLDERFUNBG_DEBUG", ...) == ...)
{
    Logging::Info(...);
}
```

This lets you enable verbose logging for one experiment without changing the
default behavior.

---

## Lab 1 — Diagnose a missing hook

1. Rename the installed hook DLL to `FolderFunBg.Hook.dll.bak`.
2. Restart Explorer.
3. Open the settings app and refresh the status.
4. What does the app report? What does the hook log say?
5. Rename the DLL back and reinstall.

Explain how the status service reaches its conclusion.

## Lab 2 — Diagnose invalid config

1. Back up `config.json`.
2. Set `"position": "diagonal"`.
3. Watch the hook log.
4. Explain why the background does not change.
5. Restore the file.

Then try a valid position and confirm hot reload works.

## Lab 3 — Read a WER report

Trigger the installed app crash by temporarily renaming
`FolderFunBg.Settings.pri` (make a backup first). Run the app and find the WER
report. What is the exception code? Which module faulted? Restore the PRI.

This lab reproduces a real packaging bug from the project's history. It teaches
you to test the installed artifact, not just the build output.

## Lab 4 — Instrument the paint path

Add a bounded counter that logs the average draw time per 100 draws. Run it in:

- an empty folder,
- a folder with many files,
- a maximized window on a high-DPI monitor.

Remove the instrumentation afterwards. What did you learn about the cost of
`AlphaBlend` versus DirectUI's own painting?

## Self-check

<details>
<summary>1. Why not log every paint call?</summary>

DirectUI repaints constantly. Logging each call adds file I/O to the render path,
which can make Explorer slow and flood the disk.
</details>

<details>
<summary>2. What is the first thing to check when no background appears?</summary>

The hook log: was MinHook installed, and was the DirectUI `PaintBackground` hook
installed? Then check the config (image exists, alpha > 0) and the folder rule.
</details>

<details>
<summary>3. Why is a minidump more useful than a screenshot?</summary>

It contains the faulting stack, modules, and exception state. A screenshot only
shows the symptom.
</details>

<details>
<summary>4. Why debug in a VM?</summary>

A bad shell extension can crash or destabilize Explorer, leaving you with a
broken desktop. A VM snapshot lets you roll back instantly.
</details>

---

## Next

[Chapter 10 — Exercises and capstones](10-exercises.md)
