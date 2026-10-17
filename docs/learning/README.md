# Learn Windows engineering with FolderFunBg

This is a hands-on course built around a real working project: per-folder
background images in File Explorer, implemented with C++, COM, Win32, DirectUI,
GDI+, C#, WinUI 3, and a self-contained installer.

It is written for college students and early-career engineers who know some
C++ or C# and want to understand **how Windows actually works** below the level
of a framework. You do not need to be an expert. You do need patience,
curiosity, and a machine or VM you can experiment on.

> **Safety:** FolderFunBg loads a DLL into `explorer.exe` and hooks an
> undocumented rendering path. Work in a VM or create a restore point. Read
> [Safety and ethics](#safety-and-ethics) before running any lab.

---

## What you will learn

By the end of this course you should be able to:

- explain how a File Explorer window is built from `HWND`s and shell views,
- describe COM, a Browser Helper Object, and how Explorer discovers one,
- explain function detours and write a safe `MinHook` hook,
- read a DirectUI element tree and paint in the correct z-order,
- use GDI and GDI+ correctly, including clipping, alpha blending, and DPI,
- design a cross-process configuration format with validation and hot reload,
- build a WinUI 3 desktop app that writes that configuration,
- package a self-contained Windows installer with an embedded payload,
- debug a native shell extension with logs, Process Explorer, Event Viewer,
  and a debugger,
- reason about the trade-offs of unsupported Windows internals.

---

## The project in one picture

```text
 FolderFunBg.Settings (C# / WinUI 3)
        │  writes config.json atomically
        ▼
 %LOCALAPPDATA%\FolderFunBg\config.json
        ▲
        │  ReadDirectoryChangesW watcher
        │
 explorer.exe
   └─ FolderFunBg.Hook.dll (C++ / COM BHO)
        ├─ detects DirectUIHWND under SHELLDLL_DefView
        ├─ MinHook detours DirectUI::Element::PaintBackground
        ├─ resolves the current folder to a rule
        └─ draws the image + readability scrim with GDI+
```

The settings app never draws anything. The hook never edits JSON. Each half has
one job, and the file is the contract.

---

## Suggested schedule

| Week | Chapters | Milestone |
|---|---|---|
| 1 | 1, 2 | Inspect Explorer's window tree; register/unregister the BHO |
| 2 | 3 | Explain and modify a MinHook detour; add a bounded log |
| 3 | 4, 5 | Trace the painting path; add a position mode |
| 4 | 6, 7 | Add a config field end-to-end and a settings page |
| 5 | 8, 9 | Build the installer; diagnose a broken build |
| 6 | 10 | Complete a capstone and write a short design note |

If you have less time, read chapters 1, 2, 4, and 6 in order, then do the
beginner exercises. If you want to contribute to the project, add chapters 3,
5, and 9.

---

## Chapters

| # | Chapter | Prerequisites | What you build or inspect |
|---|---|---|---|
| 1 | [How Explorer is built](01-how-explorer-works.md) | Basic Win32 awareness | An HWND tree walker |
| 2 | [COM and the Browser Helper Object](02-com-and-the-bho.md) | Chapter 1 | Registry inspection, registration lab |
| 3 | [Function hooking with MinHook](03-hooking-with-minhook.md) | C++ pointers, x64 basics | A bounded hook counter |
| 4 | [DirectUI and the painting path](04-directui-and-painting.md) | Chapters 1, 3 | Role probe, z-order test |
| 5 | [GDI+ images, alpha, and DPI](05-gdi-plus-images.md) | Chapter 4 | A new position mode |
| 6 | [Configuration, rules, and hot reload](06-config-and-hot-reload.md) | C# or C++ JSON | An `enabled` flag end-to-end |
| 7 | [The WinUI 3 settings app](07-winui3-settings-app.md) | C#, XAML basics | A diagnostics page |
| 8 | [Packaging and installers](08-packaging-and-installers.md) | Chapter 2 | A verified installer build |
| 9 | [Debugging and observability](09-debugging-and-observability.md) | Chapters 3–5 | A diagnosed failure |
| 10 | [Exercises and capstones](10-exercises.md) | All of the above | A portfolio-sized extension |

Reference material:

- [Code tour](code-tour.md) — read the repository in a sensible order.
- [Glossary](glossary.md) — COM, GDI, DirectUI, and shell terms.
- [Further reading](further-reading.md) — official documentation and books.
- [Install guide](../install-guide.md) — end-user setup with screenshots.

---

## How to use the labs

Each chapter ends with **labs** and **self-check questions**. Labs are designed
to be done in a VM or on a disposable install:

1. Read the chapter once without touching the code.
2. Complete the lab, keeping the change small.
3. Run `.\build.ps1` and the relevant smoke test.
4. Revert the lab change before starting the next chapter, unless the chapter
   says to keep it.

When a lab needs a rebuild of the Explorer hook, remember that Explorer must be
restarted to load the new DLL. The setup EXE and `install.ps1` do that for you.

### Build commands

```powershell
.\build.ps1                 # native hook + C# app + tests + smoke test staging
.\build-native.cmd          # faster native-only build
.\build-installer.ps1       # publish the app and build install\FolderFunBg-Setup.exe
dotnet test tests\FolderFunBg.Core.Tests\FolderFunBg.Core.Tests.csproj
```

The hook log is your primary debugging tool:

```text
%LOCALAPPDATA%\FolderFunBg\Logs\FolderFunBg.log
```

Keep it open in a terminal:

```powershell
Get-Content "$env:LOCALAPPDATA\FolderFunBg\Logs\FolderFunBg.log" -Wait
```

---

## Safety and ethics

- Use a VM or a restore point. Shell extensions run inside your desktop session.
- Do not install two tools that hook Explorer's drawing path at the same time.
- Never use hooking to hide activity, bypass security controls, or modify
  another user's machine without permission.
- The DirectUI exports used here are undocumented and version-specific. Treat
  them as research, not as a stable platform API.
- Code signing is strongly recommended for binaries you distribute.

---

## A note on learning from a real repository

Real projects are messier than tutorials. This one intentionally keeps the
scope small, but it still contains compromises:

- The C# config model and the C++ parser are separate implementations that must
  stay in sync.
- The DirectUI hook uses decorated C++ export names, which change between
  builds; the GDI fallback exists for that reason.
- The installer replaces a DLL that Explorer may have loaded, which requires a
  careful stop–copy–restart sequence.
- There is a documented WinUI packaging bug (a missing PRI file) that the build
  script works around.

Reading these decisions and their alternatives is part of the course.
