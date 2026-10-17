# Chapter 7 — The WinUI 3 settings app

**Learning goals**

- Explain the difference between packaged and unpackaged WinUI 3 apps.
- Trace an app from `OnLaunched` to a `NavigationView` page.
- Separate services (files, registry, pickers) from UI code.
- Add a page and a diagnostic workflow without breaking the smoke test.

**Prerequisites:** Chapter 6 and basic C#/XAML.

---

## 7.1 WinUI 3 and the Windows App SDK

WinUI 3 is Microsoft's modern native UI framework for Windows, shipped as part
of the Windows App SDK. It is not UWP: the app can be a normal Win32 desktop
executable.

Two deployment models:

| Model | Identity | Install | Used here |
|---|---|---|---|
| Packaged (MSIX) | Package identity, app container | Store/MSIX | No |
| Unpackaged | No package identity | Plain EXE | Yes |

The csproj sets:

```xml
<WindowsPackageType>None</WindowsPackageType>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

`WindowsPackageType=None` produces an unpackaged app. `WindowsAppSDKSelfContained`
copies the Windows App SDK binaries next to the app, so the target does not need
the Windows App Runtime installed. The installer additionally publishes the
.NET runtime self-contained.

## 7.2 Startup

`src/FolderFunBg.Settings/App.xaml.cs`:

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    AppServices.Initialize();
    var window = new MainWindow();
    CurrentWindow = window;
    window.Activate();

    if (args contains --smoke-test)
    {
        _ = RunSmokeTestAsync(window);
    }
}
```

`AppServices.Initialize()` loads the config, resolves the images directory, and
creates the image library. If loading fails, it records `StartupError` and falls
back to a default config so the app still opens.

The smoke test exists because UI tests are hard. It navigates every page,
waits 400 ms per page, writes `%TEMP%\FolderFunBg-smoke.txt`, closes the window,
and exits with code 1 on failure. It is not a unit test; it is a *canary* that
catches XAML/resource/binding errors on a machine with no test runner.

## 7.3 Navigation

`MainWindow.xaml` uses a `NavigationView`. Each item has a `Tag`:

```xml
<NavigationViewItem Content="Folders" Tag="folders" Icon="Folder" />
<NavigationViewItem Content="Image library" Tag="library" Icon="Pictures" />
<NavigationViewItem Content="Defaults" Tag="defaults" Icon="Setting" />
<NavigationViewItem Content="Settings" Tag="settings" Icon="Repair" />
```

`NavView_SelectionChanged` maps the tag to a page type and calls
`ContentFrame.Navigate(pageType)`. This is a deliberately small navigation
service; there is no MVVM framework.

Why not use a full MVVM toolkit? The app has four simple pages. Introducing a
framework would add concepts without teaching the Windows-specific parts. The
code instead shows the raw patterns: services, `x:Bind`, and code-behind event
handlers.

## 7.4 Services and data

| Service | Responsibility |
|---|---|
| `AppServices` | Load/save config, expose paths, resolve images |
| `ExtensionStatusService` | Read COM registration and detect whether the DLL is loaded in Explorer |
| `PickerHelper` | File/folder pickers that need the window handle |
| `ConfigStore` | Atomic JSON read/write and validation |
| `ImageLibrary` | Import, list, and delete images with signature validation |

`ExtensionStatusService` is worth studying: it combines three sources of truth.

```csharp
// 1. The registered COM path
Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InProcServer32");

// 2. The file exists
File.Exists(dllPath);

// 3. The DLL is loaded in a running Explorer process
process.Modules.Any(module => module.ModuleName == "FolderFunBg.Hook.dll");
```

Checking only the registry is not enough: a registration can point to a deleted
file, and a valid registration does not prove Explorer restarted and loaded it.
`process.Modules` can throw for protected processes or a process that just
exited; the code handles those cases.

## 7.5 Models and binding

`Pages/FoldersPage.xaml.cs` uses `x:Bind` with small model classes:

- `FolderRuleItem` — display name, path, image, position, alpha, recursive,
  enabled,
- `ImageItem` — file name, path, preview bitmap.

`x:Bind` is compile-time binding: it generates code and fails the build if a
property does not exist. It is faster than classic `Binding` and catches typos
early. The trade-off is that you must tell the compiler the data type
(`x:DataType`).

When the collection changes, the page updates the `ObservableCollection`. When a
user edits a field, the page copies values back into the `FolderRule` model and
calls `AppServices.Save()`.

## 7.6 UI lessons hidden in this app

- **Unpackaged apps must initialize the Windows App SDK.** The generated
  bootstrap code handles this when `WindowsPackageType=None`.
- **Pickers need a window handle.** `PickerHelper` uses
  `WinRT.Interop.InitializeWithWindow`; a picker without an owner window can
  fail on desktop.
- **Theme colors:** use `ThemeResource` brushes instead of hard-coded colors.
  The app follows the system light/dark theme.
- **Accessibility:** `NavigationViewItem` content becomes the accessible name.
  Buttons have text. If you add icon-only controls, set `AutomationProperties.Name`.
- **Layout:** fixed-width controls inside a stretching `StackPanel` can center
  unexpectedly, and a wrapping `TextBlock` measured with infinite width can
  stretch an `Auto` column. The Defaults page constrains its form column to 360
  pixels and sets `HorizontalAlignment="Left"` on the controls. This was a real
  bug caught while taking documentation screenshots.

## 7.7 Smoke tests vs. unit tests

The core logic is tested with xUnit (43 tests). The UI is tested with the smoke
run. This split is intentional:

- Core tests run fast on CI and cover validation, paths, rules, and image
  library behavior.
- The smoke test catches resource and XAML failures that unit tests cannot.

Neither replaces manual testing on real Explorer. The compatibility document
contains the manual matrix: tabs, DPI, multiple monitors, dark theme, missing
images, and so on.

---

## Lab 1 — Add a Diagnostics page

Add a fifth page that shows:

- the config path and whether it exists,
- the installed DLL path and whether it exists,
- whether the DLL is loaded in Explorer,
- the last 20 lines of the hook log,
- a **Copy diagnostics** button.

This exercises navigation, new XAML, services, and file I/O. Update the smoke
test to include the new page. Keep the page read-only; diagnostics should never
change state.

## Lab 2 — Make the Defaults page responsive

Open the Defaults page and resize the window from 1000 px to 1600 px wide. The
form and the preview currently sit side by side. Add a responsive layout that
stacks the preview below the form when the window is narrow (for example with a
`VisualStateManager` or a second `Grid` definition). Verify at 900, 1200, and
1600 px. Keep the preview usable at high DPI.

## Lab 3 — Add an `enabled` toggle

Wire the config `enabled` flag from Chapter 6 into the Defaults page:

- add a `ToggleSwitch`,
- bind it to the config model,
- save it,
- reflect it in the UI after loading.

Handle the "not saved yet" state: the app should not claim the change is applied
until **Save defaults** succeeds.

## Lab 4 — Accessibility pass

Run **Accessibility Insights for Windows** (Microsoft) against the app. Fix:

- missing accessible names,
- contrast issues in the preview,
- keyboard navigation order,
- focus indicators.

Write down what you changed and why. This makes a strong portfolio note.

## Self-check

<details>
<summary>1. What does <code>WindowsPackageType=None</code> change?</summary>

It builds an unpackaged desktop app with no MSIX package identity. The app runs
as a normal EXE and must carry its own Windows App SDK runtime if
self-contained.
</details>

<details>
<summary>2. Why does the status check inspect running processes?</summary>

A registry entry only proves registration. It does not prove the DLL exists or
that Explorer loaded it. Checking the module list verifies the running state.
</details>

<details>
<summary>3. Why use <code>x:Bind</code> instead of <code>Binding</code>?</summary>

`x:Bind` is compiled, faster, and type-checked at build time. It requires a
declared data type but catches errors earlier.
</details>

<details>
<summary>4. Why is the smoke test not a unit test?</summary>

It launches the real UI and navigates pages. It is slower and environment
dependent, but it catches XAML/resource failures that unit tests cannot.
</details>

---

## Next

[Chapter 8 — Packaging and installers](08-packaging-and-installers.md)
