# Code tour

This tour tells you what to read, in what order, and what to notice. It is more
useful than opening files alphabetically. Each section ends with a question you
should be able to answer before moving on.

---

## 0. The map

```text
src/
  FolderFunBg.Core/        C# config, validation, rules, image library
  FolderFunBg.Hook/        C++ COM BHO, hooks, rendering, config parsing
  FolderFunBg.Settings/    WinUI 3 settings app
tests/
  FolderFunBg.Core.Tests/  43 xUnit tests
installer/
  *.ps1                    elevated install/uninstall scripts
  FolderFunBg.Installer/   setup EXE project
docs/
  install-guide.md         end-user setup
  architecture.md          component and hook flow
  compatibility.md         support matrix and manual tests
  tutorial.md              quick learner tour
  learning/                this course
```

---

## 1. Contract first: the config model

Read these in order:

1. `src/FolderFunBg.Core/FolderFunBgConfig.cs`
2. `src/FolderFunBg.Core/BackgroundDefaults.cs`
3. `src/FolderFunBg.Core/FolderRule.cs`
4. `src/FolderFunBg.Core/ReadabilitySettings.cs`
5. `src/FolderFunBg.Core/BackgroundPositions.cs`

**Notice:** the model is intentionally small and nullable where inheritance is
allowed. `FolderRule` uses `string?` and `int?` for overrides.

**Question:** why is `FolderRule.Alpha` an `int?` instead of an `int`?

<details>
<summary>Answer</summary>

`null` means "inherit from defaults". `0` is a valid alpha (invisible image), so
it cannot double as "unspecified".
</details>

Continue with:

6. `src/FolderFunBg.Core/ConfigValidator.cs`
7. `src/FolderFunBg.Core/PathRuleResolver.cs`
8. `src/FolderFunBg.Core/ConfigStore.cs`

**Notice:** validation is separate from parsing. The resolver is pure and
testable. `ConfigStore` writes atomically.

**Question:** which wins, an exact path rule or a longer recursive rule? Where is
that implemented?

---

## 2. The native boundary

Read:

1. `src/FolderFunBg.Hook/dllmain.cpp`
2. `src/FolderFunBg.Hook/ComBho.h`
3. `src/FolderFunBg.Hook/ComBho.cpp`
4. `src/FolderFunBg.Hook/AppState.h`
5. `src/FolderFunBg.Hook/AppState.cpp`

**Notice:** the loader guard, the two COM objects, the `DocumentComplete`
handler, and the lifecycle in `AppState::Start`/`Stop`.

**Question:** what keeps the hook alive after the browser object is released? Is
that intentional?

Then read:

6. `src/FolderFunBg.Hook/Config.h`
7. `src/FolderFunBg.Hook/Config.cpp`
8. `src/FolderFunBg.Hook/PathUtils.cpp`

**Notice:** the native parser duplicates the C# schema. Look for normalization
and validation differences. This is the risk area.

**Question:** what happens when a config file is invalid? Does the hook keep the
old config or fall back to defaults?

---

## 3. Follow one paint

Trace the path with a debugger or a notebook:

```text
DocumentComplete (ComBho.cpp)
  └─ SetLocationForThread (Hooks.cpp)
BeginPaint (Hooks.cpp)
  └─ records the HDC for the tracked window
DirectUI::Element::PaintBackground (Hooks.cpp)
  └─ HookPaintBackground
       ├─ GetAccRole       (role 33/34)
       ├─ GetRoot
       ├─ GetRootRelativeBounds
       └─ DrawConfiguredBackground
            ├─ AppState::GetConfig
            ├─ Config::Resolve(location)
            ├─ ImageCache::Get
            ├─ CachedImage::Draw (AlphaBlend)
            └─ DrawReadabilityScrim
```

Read `src/FolderFunBg.Hook/Hooks.cpp` alongside this trace. Mark every lock and
every early return.

**Question:** why is `g_duiDrewForHdc` `thread_local`? What would happen if it
were a single global?

<details>
<summary>Answer</summary>

Explorer's UI threads paint concurrently. A single global would make one
thread's draw suppress another thread's draw. `thread_local` keeps one draw per
HDC per thread.
</details>

---

## 4. Images and GDI+

Read:

1. `src/FolderFunBg.Hook/ImageCache.h`
2. `src/FolderFunBg.Hook/ImageCache.cpp`

**Notice:**

- signature validation before GDI+ decode,
- `IStream` ownership,
- memory DC creation,
- luminance sampling,
- per-image draw mutex,
- failure caching.

**Question:** why is the image cache cleared when the config changes?

<details>
<summary>Answer</summary>

The config can point to a different file with the same name, or the file can be
replaced. Clearing the cache ensures the next draw loads the current bytes.
</details>

---

## 5. The settings app

Read in this order:

1. `src/FolderFunBg.Settings/App.xaml.cs`
2. `src/FolderFunBg.Settings/MainWindow.xaml` and `.cs`
3. `src/FolderFunBg.Settings/Services/AppServices.cs`
4. `src/FolderFunBg.Settings/Services/ExtensionStatusService.cs`
5. `src/FolderFunBg.Settings/Pages/DefaultsPage.xaml` and `.cs`
6. `src/FolderFunBg.Settings/Pages/FoldersPage.xaml` and `.cs`
7. `src/FolderFunBg.Settings/Pages/LibraryPage.xaml` and `.cs`
8. `src/FolderFunBg.Settings/Pages/SettingsPage.xaml` and `.cs`

**Notice:** the smoke-test mode, the tag-based navigation, the service boundary,
and the `x:Bind` models.

**Question:** why does `ExtensionStatusService` inspect process modules instead
of trusting the registry?

---

## 6. Tests

Read `tests/FolderFunBg.Core.Tests/`. Identify tests for:

- validation failures,
- path normalization,
- exact vs recursive precedence,
- CLSID rules,
- readability merging,
- atomic saves (if covered),
- legacy config import.

**Question:** which behavior is not covered by unit tests because it lives in
C++ or requires a live Explorer? How would you test it?

---

## 7. Installer and build

Read:

1. `installer/FolderFunBg.Installer/FolderFunBg.Installer.csproj`
2. `installer/FolderFunBg.Installer/Program.cs`
3. `build-installer.ps1`
4. `installer/install.ps1` and `installer/uninstall.ps1`

**Notice:** the elevation manifest, embedded payload, zip-slip check, stop/copy/
restart sequence, PRI workaround, and checksum generation.

**Question:** why does the installer stop Explorer twice around registration?

---

## 8. Follow one config save

```text
DefaultsPage.Save_Click
  └─ AppServices.Config.* = ...
  └─ AppServices.Save()
       └─ ConfigStore.Save()
            ├─ ConfigValidator.ValidateOrThrow
            ├─ write config.json.tmp-<guid>
            └─ File.Move(..., overwrite: true)
ConfigWatcher (explorer.exe)
  └─ FindFirstChangeNotification
       └─ ReloadConfig
            └─ Config::LoadFromFile
                 └─ swap shared_ptr<const Config>
                      └─ Hooks::InvalidateAll()
```

Read the three files in that chain. Then explain why the watcher might fire more
than once for a single save.

---

## 9. Follow one installation

```text
FolderFunBg-Setup.exe (elevated)
  ├─ read embedded FolderFunBg.Payload.zip
  ├─ extract to %TEMP% (zip-slip checked)
  ├─ stop explorer.exe and FolderFunBg.Settings
  ├─ copy Hook/FolderFunBg.Hook.dll to %ProgramFiles%\FolderFunBg
  ├─ copy Settings\** to %ProgramFiles%\FolderFunBg\Settings
  ├─ regsvr32 /s FolderFunBg.Hook.dll
  ├─ create Start Menu shortcut via WScript.Shell
  └─ stop Explorer again, start a fresh explorer.exe
```

Read `Program.cs` and map each step to a method. Then list the failure points
and what the log records.

---

## 10. Reading questions for a code review

Use these when reviewing your own changes or someone else's:

1. Does the change add work to a paint path? Is it bounded?
2. Does it hold a lock while doing I/O or parsing?
3. Can it throw across a C callback boundary?
4. Does it change the config schema? Are both parsers updated?
5. Does it add a user-visible string without accessibility metadata?
6. Does it assume a DirectUI export or paint order? Is there a fallback?
7. Does it leave a temporary probe or log line in production?
8. Does the installer still work when Explorer is running?
9. Is the app still self-contained after publish?
10. Does the smoke test cover the new page?

---

## Suggested deep-dive order by interest

| Interest | Read |
|---|---|
| Windows internals | Chapters 1, 2, 4; `Hooks.cpp`, `ComBho.cpp` |
| Graphics | Chapter 5; `ImageCache.cpp`, GDI+ documentation |
| Product engineering | Chapters 6, 7, 8; `ConfigStore`, pages, installer |
| Debugging/security | Chapter 9; WER, WinDbg, installer payload handling |
| Testing/CI | Tests, smoke test, `.github/workflows/build.yml` |

---

Back to the [learning index](README.md).
