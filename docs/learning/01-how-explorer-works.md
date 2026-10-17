# Chapter 1 — How File Explorer is built

**Learning goals**

- Read a Windows window tree and identify the windows that make up Explorer.
- Explain the difference between a Win32 control and a DirectUI view.
- Find the folder a shell view is displaying.
- Understand why FolderFunBg needs a hook instead of an official API.

**Prerequisites:** you can read C or C# and have used Windows. No COM needed yet.

---

## 1.1 A window is a handle, not an object you own

Win32 is a C API. The operating system owns the real window objects; your
program receives an opaque `HWND` (handle) and asks the system to do things with
it. Handles are process-scoped, so you should not send an `HWND` from one
process to another and expect it to mean the same window.

Every window has:

- a **class name** (`GetClassName`), such as `CabinetWClass`,
- a **window title** (`GetWindowText`),
- a **parent** (`GetParent`),
- a **thread and process** that created it (`GetWindowThreadProcessId`),
- a **style** and an **extended style** (`GetWindowLongPtr`).

Most Windows UI is a tree of these handles. A dialog contains buttons; a button
is a window. The tree is not necessarily the same as the logical UI tree, but
for Explorer it is usually close.

## 1.2 The Explorer window tree

A modern File Explorer window looks approximately like this:

```text
CabinetWClass                     top-level window (the frame)
└─ ShellTabWindowClass            tab host
   └─ SHELLDLL_DefView            the shell view for the current folder
      ├─ DirectUIHWND             the file list and its UI
      ├─ DirectUIHWND             navigation pane (a separate host in many builds)
      ├─ DirectUIHWND             command bar
      └─ ...                      preview pane, status bar, ...
```

Two names matter for this project:

- **`SHELLDLL_DefView`** is the shell view. It is the window that hosts a
  folder's contents.
- **`DirectUIHWND`** is the window that hosts a **DirectUI** element tree.

There is no `Edit` or `SysListView32` here. The file list is not a classic Win32
list-view control. It is a `DirectUIHWND` plus an internal `DirectUI::Element`
tree rendered by `dui70.dll`. That is why `SendMessage(LVM_GETITEMCOUNT)` and
similar classic control messages do not work on it.

> **Why this matters:** FolderFunBg must draw into a window whose contents are
> rendered by an undocumented toolkit. The only practical insertion point is a
> function inside that toolkit.

### Tabs

Each tab creates its own `SHELLDLL_DefView` and `DirectUIHWND` under the tab
window. `DOCUMENTCOMPLETE` fires for the active tab's navigation, and the hook
tracks windows per Explorer thread. This is why tab support mostly works, but it
is also why you should test tab transitions explicitly.

## 1.3 Finding the current folder

Explorer's BHO receives `DWebBrowserEvents2::DocumentComplete` with the
navigated URL, usually a `file:///C:/Users/...` URL. `ComBho.cpp` converts it
with `PathCreateFromUrlW` and stores the result per thread:

```cpp
Hooks::SetLocationForThread(GetCurrentThreadId(), location);
```

The paint code later looks up the location for the painting thread. This is a
simple design with a useful property: no cross-thread COM marshalling is needed.

Shell namespace folders (This PC, Recycle Bin) are not filesystem paths. They
are identified by a **CLSID**, for example:

```text
::{20D04FE0-3AEA-1069-A2D8-08002B30309D}   This PC
::{645FF040-5081-101B-9F08-00AA002F954E}   Recycle Bin
```

FolderFunBg accepts CLSID rules in `config.json` so those folders can have their
own backgrounds.

## 1.4 Why there is no official per-folder background API

Windows exposes APIs for many shell visual features:

- `desktop.ini` for folder icons, tooltips, and localized names,
- `IShellView` for hosting a custom view,
- thumbnail and icon providers,
- `IFolderView` for querying items.

There is no supported API for "paint an image behind the items in this folder".
The old `desktop.ini` `IconArea_Image` key is no longer honored on current
Windows 11 builds. That leaves three broad strategies:

| Strategy | Difficulty | Stability | Used by FolderFunBg? |
|---|---|---|---|
| Hook an internal drawing function | High | Low (version-sensitive) | Yes, primary |
| Wrap `IShellView` and draw yourself | Very high | Medium–high | Not yet |
| Composition/DWM integration | Very high | Low–medium | Not yet |

FolderFunBg deliberately chooses the smallest working intervention and keeps a
second rendering path (`FillRect`) as a fallback.

---

## Lab 1 — Walk the window tree

**Goal:** prove to yourself that the tree above is real.

1. Open a File Explorer window.
2. Download **Process Explorer** from Sysinternals (Microsoft Learn).
3. Select `explorer.exe`, press `Ctrl+F`, and search for `DirectUIHWND`.
4. Inspect the parent chain. You should find `SHELLDLL_DefView` and a tab/frame
   window above it.
5. Inspect the **Handles** pane if you are curious; window handles appear as
   `Window` handles.

**Bonus:** write a small C# console app that prints the tree:

```csharp
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    private static void Main()
    {
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (Path.GetFileNameWithoutExtension(GetProcessName(pid)) == "explorer")
            {
                Print(hwnd, 0);
            }
            return true;
        }, IntPtr.Zero);
    }

    private static string GetProcessName(uint pid)
    {
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return ""; }
    }

    private static void Print(IntPtr hwnd, int depth)
    {
        var cls = new StringBuilder(256);
        var title = new StringBuilder(256);
        GetClassName(hwnd, cls, cls.Capacity);
        GetWindowText(hwnd, title, title.Capacity);
        Console.WriteLine($"{new string(' ', depth * 2)}{cls} \"{title}\" 0x{hwnd:X}");
        EnumChildWindows(hwnd, (child, _) => { Print(child, depth + 1); return true; }, IntPtr.Zero);
    }
}
```

Run it with `dotnet run`. Compare the output with Process Explorer.

## Lab 2 — Tabs and navigation

1. Open one Explorer window with two tabs on different folders.
2. Use your tree walker or Process Explorer to compare the two tab trees.
3. Navigate one tab while watching the hook log:

```powershell
Get-Content "$env:LOCALAPPDATA\FolderFunBg\Logs\FolderFunBg.log" -Wait
```

You should see `Config loaded` only when the config changes, and `DUI-DRAW`
entries as the view repaints. Note how many `SHELLDLL_DefView` windows exist at
once.

## Self-check

<details>
<summary>1. Why does <code>LVM_GETITEMCOUNT</code> not work on the file list?</summary>

Because the file list is not a classic `SysListView32` control. It is a
`DirectUIHWND` hosting a DirectUI element tree, rendered by `dui70.dll`.
</details>

<details>
<summary>2. Where does the current folder path come from?</summary>

From `DWebBrowserEvents2::DocumentComplete`, which provides a URL. The BHO
converts it with `PathCreateFromUrlW` and stores it per Explorer thread.
</details>

<details>
<summary>3. Why is <code>GetParent</code> not enough to find the folder?</summary>

The window tree tells you which view is hosted, not which folder is displayed.
The folder comes from the navigation event, not from the HWND itself.
</details>

<details>
<summary>4. What is a CLSID rule used for?</summary>

Special shell folders such as This PC or the Recycle Bin that are not normal
filesystem paths. They are identified by a GUID instead of a path.
</details>

---

## Next

[Chapter 2 — COM and the Browser Helper Object](02-com-and-the-bho.md)
