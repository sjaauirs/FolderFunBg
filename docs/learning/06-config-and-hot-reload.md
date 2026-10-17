# Chapter 6 — Configuration, rules, and hot reload

**Learning goals**

- Design a small JSON schema that two languages can implement consistently.
- Validate untrusted input at the process boundary.
- Explain exact vs recursive rule precedence and partial overrides.
- Implement cross-process hot reload with a file watcher and an immutable
  config snapshot.

**Prerequisites:** Chapters 2 and 4. Basic C# and C++.

---

## 6.1 The schema

`config.json` lives at `%LOCALAPPDATA%\FolderFunBg\config.json`:

```json
{
  "version": 1,
  "imagesRoot": "%LOCALAPPDATA%\\FolderFunBg\\Images",
  "defaults": {
    "image": "krishna.png",
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
      "path": "D:\\Downloads",
      "image": "downloads.png",
      "position": "bottom-left",
      "alpha": 180,
      "recursive": false,
      "enabled": true,
      "readability": { "mode": "light", "strength": 120 }
    },
    {
      "clsid": "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
      "image": "this-pc.png"
    }
  ]
}
```

Design decisions:

- **File names, not absolute paths.** An image is a file name inside the managed
  library. Absolute paths and path traversal are rejected.
- **Defaults plus partial overrides.** A rule only needs the fields it changes.
- **Environment variables are expanded** for `imagesRoot` and folder paths.
- **Backward-compatible version field.** The current reader ignores unknown
  fields, which allows additive changes.

## 6.2 Two implementations, one contract

There are two parsers:

- `src/FolderFunBg.Core/` — C# models and validation for the settings app.
- `src/FolderFunBg.Hook/Config.cpp` — the native parser used inside Explorer.

Why duplicate? The hook cannot run .NET inside Explorer just to read JSON, and
the WinUI app should not P/Invoke into the native parser for every edit. The
file is the contract, and both sides must enforce the same rules.

The duplication is a maintenance risk. The repository mitigates it with:

- a shared sample config (`samples/config.example.json`),
- unit tests for the C# side,
- consistent normalization rules (case-insensitive paths, trimmed separators),
- a validation error message that names the offending field.

If you add a field, update both parsers and both tests. This is a good
opportunity to practice keeping two implementations honest.

## 6.3 Validation at the boundary

Validation rules in `ConfigValidator` and `Config.cpp`:

| Field | Rule |
|---|---|
| `image` | File name ending in `.png`, `.jpg`, or `.jpeg`; no path separators |
| `position` | One of the known position strings |
| `alpha` | 0–255 |
| `readability.mode` | `auto`, `none`, `light`, or `dark` |
| `readability.strength` | 0–255 |
| `path` | Absolute after environment expansion and normalization |
| `clsid` | A valid GUID, normalized to `::{GUID}` |
| `folders` | Exactly one identifier per rule (`path` or `clsid`) |

Invalid config is rejected as a whole on the native side. The hook logs the
error and keeps the previous known-good config. Failing closed is safer than
drawing with half-parsed data.

```cpp
try
{
    auto loaded = Config::LoadFromFile(path);
    {
        std::unique_lock lock(configMutex_);
        config_ = std::move(loaded);
    }
    imageCache_.Clear();
}
catch (const std::exception& exception)
{
    Logging::Error(L"Config load failed: " + ...);
}
```

## 6.4 Rule resolution

`PathRuleResolver` and `Config::Resolve` implement the same algorithm:

1. Normalize the current location.
2. If it is a CLSID, find an enabled CLSID rule.
3. Otherwise, find:
   - an exact path rule, else
   - the longest enabled recursive rule whose path is a prefix of the location.
4. Merge the rule over the defaults:
   - image/position/alpha/random fall back when unspecified,
   - readability is merged field by field.

Example:

```text
defaults: bottom-right, alpha 255, auto
rule D:\Downloads: position bottom-left, alpha 180
rule D:\ (recursive): image desktop.png
```

Opening `D:\Downloads\2026` resolves to:

- image `desktop.png` (recursive rule),
- position `bottom-left` and alpha `180` (exact rule),
- readability `auto` (defaults).

Exact rules beat recursive rules, and among recursive rules the longest path
wins. This is the same mental model as firewall or routing tables.

## 6.5 Atomic writes

The settings app never writes `config.json` in place. It writes a temporary file
in the same directory and moves it over the target:

```csharp
File.WriteAllText(tempPath, json, utf8NoBom);
File.Move(tempPath, ConfigPath, overwrite: true);
```

A move within the same volume is atomic on NTFS. The watcher may see a temporary
file or a rename event, but it will never read a half-written document.

This pattern matters for cross-process contracts. Use it whenever another
process polls or watches your file.

## 6.6 Hot reload

`ConfigWatcher` uses `FindFirstChangeNotificationW` on the config directory with
these filters:

```cpp
FILE_NOTIFY_CHANGE_LAST_WRITE |
FILE_NOTIFY_CHANGE_FILE_NAME |
FILE_NOTIFY_CHANGE_SIZE
```

When a change arrives, it sleeps 150 ms (debounce), reloads the config, and
calls `Hooks::InvalidateAll()` to repaint tracked windows.

The config pointer is a `shared_ptr<const Config>` guarded by a
`std::shared_mutex`:

- readers take a shared lock,
- the watcher takes an exclusive lock and swaps the pointer,
- old snapshots are destroyed when the last reader releases them.

This avoids locking the paint path for the duration of a JSON parse. It also
means a paint operation always sees a complete, immutable config.

`InvalidateAll()` repaints every tracked folder window:

```cpp
InvalidateRect(hwnd, nullptr, TRUE);
```

Without this, the new config would only appear on the next natural repaint
(navigation, resize, or scroll).

## 6.7 Failure modes

| Failure | Behavior |
|---|---|
| Config file missing | Defaults are used; no background until an image is chosen |
| Invalid JSON | Error logged; previous config remains active |
| Image file missing | Draw is skipped; failure is logged once |
| Corrupt image | Signature check rejects it; no GDI+ decode |
| Watcher directory unavailable | Hook runs with the last loaded config |
| Write interrupted | Atomic move prevents a partial file |

## Lab 1 — Add an `enabled` flag

Add a global `enabled` boolean to the schema:

1. Add it to `FolderFunBgConfig` and the C++ config model.
2. Parse and validate it in both languages.
3. Make the paint path return early when it is false.
4. Add a toggle to the Defaults page.
5. Add unit tests.
6. Test hot reload: toggle it while Explorer is open.

This lab touches every layer and is the best rehearsal for contributing a real
feature.

## Lab 2 — Break the config on purpose

With FolderFunBg running, replace `config.json` with:

```json
{ "version": 1, "defaults": { "alpha": "not a number" } }
```

Watch the hook log. Does the last good background remain? What error message do
you get? Restore the file. Then do the same with invalid JSON syntax.

## Lab 3 — Measure the watcher

Add a log line to the watcher callback with a timestamp. Save the config ten
times quickly from the settings app. How many callbacks arrive? How much does
the 150 ms debounce collapse? Remove the log afterwards.

## Self-check

<details>
<summary>1. Why is the config pointer immutable and swapped?</summary>

So the paint path never observes a partially updated object. Readers hold a
shared pointer to a complete snapshot; the watcher swaps in a new one.
</details>

<details>
<summary>2. Which rule wins: an exact rule or a longer recursive rule?</summary>

An exact path match wins. If there is no exact match, the longest enabled
recursive rule wins.
</details>

<details>
<summary>3. Why is an atomic file move better than writing in place?</summary>

A watcher can read the file at any time. Writing in place exposes partial JSON.
A same-volume move publishes the new content atomically.
</details>

<details>
<summary>4. Why keep the previous config on a parse error?</summary>

Failing closed to a known-good state keeps Explorer usable and makes the error
recoverable. Replacing the config with defaults would silently remove the user's
backgrounds.
</details>

---

## Next

[Chapter 7 — The WinUI 3 settings app](07-winui3-settings-app.md)
