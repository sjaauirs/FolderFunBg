# Chapter 4 — DirectUI and the painting path

**Learning goals**

- Read a DirectUI `Element` tree and its accessibility roles.
- Explain the paint order that lets an image appear *behind* icons and names.
- Resolve the undocumented `PaintBackground` export and call it safely.
- Handle empty folders, tabs, and multiple HDCs correctly.

**Prerequisites:** Chapters 1 and 3. You should understand a detour and an HDC.

---

## 4.1 What DirectUI is

DirectUI is Microsoft's internal retained-mode UI toolkit, implemented in
`dui70.dll`. Modern Explorer uses it for the file list, navigation pane, command
bar, and other surfaces. A `DirectUIHWND` is the Win32 host window; the actual
UI is a tree of `DirectUI::Element` objects.

Elements have:

- a parent and children (`GetParent`, `GetRoot`),
- a position and size relative to the root (`GetRootRelativeBounds`),
- an accessibility role (`GetAccRole`), using the same constants as MSAA,
- optional properties, styles, and event handlers.

The toolkit is **not documented as a public API**. The class names, decorated
method names, and even the paint order can change between Windows builds. Treat
everything in this chapter as version-specific research.

Useful roles for this project:

| Constant | Value | Meaning in Explorer |
|---|---|---|
| `ROLE_SYSTEM_LIST` | 33 | The list view itself; also paints in empty folders |
| `ROLE_SYSTEM_LISTITEM` | 34 | A file row |
| `ROLE_SYSTEM_OUTLINEITEM` | 36 | A navigation-pane tree item |
| `ROLE_SYSTEM_PANE` | 10 | A generic pane/container |

FolderFunBg triggers on roles 33 and 34. Role 33 was added after testing showed
that empty folders never paint a list item, so a list-item-only hook produced no
background. This is a good example of a bug found by a targeted test case.

## 4.2 The decorated export name

C++ compilers encode a function's class, name, and signature in the exported
symbol. The DirectUI method is:

```text
?PaintBackground@Element@DirectUI@@QEAAXPEAUHDC__@@PEAVValue@2@AEBUtagRECT@@222@Z
```

Read it as:

- `Element@DirectUI` — class `DirectUI::Element`,
- `PaintBackground` — method name,
- `QEAAX` — public, non-static, `__cdecl`/x64, returns `void`,
- `PEAUHDC__@@` — pointer to `HDC__` (an HDC),
- `PEAVValue@2@` — pointer to `DirectUI::Value`,
- `AEBUtagRECT@@222` — references to four `RECT`s.

The signature matched in `Hooks.cpp` is:

```cpp
using PaintBackground_t =
    void(__fastcall*)(void*, HDC, void*, const RECT*,
                      const RECT*, const RECT*, const RECT*);
```

The first `void*` is the `this` pointer. The remaining arguments are the HDC, a
value/style pointer, and four rectangles (bounds, clip, exclude, target).

> If the symbol is not found, `InstallDuiBackground()` logs an error and the
> project falls back to the GDI path. It does not guess.

## 4.3 Why the image appears behind the icons

DirectUI paints a view in layers. A simplified order:

```text
1. The list element paints its background (empty area, selection color).
2. Our PaintBackground hook runs after the original list background.
3. DirectUI paints each file item: icon, text, focus rectangle.
```

Because the hook draws at step 2, everything in step 3 lands on top. The image
is behind the icons and filenames even if it is centered or stretched.

The old `FillRect` hook draws during the GDI erase pass. On Windows 11 build
26220 that layer is composited above the DirectUI output, so the image can cover
text. That is the exact limitation the DirectUI hook solves.

## 4.4 The hook implementation

The core of `HookPaintBackground` is deliberately small:

```cpp
const int role = g_getAccRole(self);
if (role != ROLE_SYSTEM_LIST && role != ROLE_SYSTEM_LISTITEM)
{
    return;
}

if (hdc != g_duiHdc)          // new paint pass?
{
    g_duiHdc = hdc;
    g_duiDrewForHdc = false;
}

if (g_duiDrewForHdc)          // draw once per HDC
{
    return;
}

void* root = g_getRoot(self);
RECT bounds{};
g_getRootRelativeBounds(root, &bounds);

if (bounds are large enough)
{
    g_duiDrewForHdc = true;
    DrawConfiguredBackground(hdc, bounds, role);
}
```

Important details:

- **One draw per HDC.** `PaintBackground` is called for many elements and many
  items. Without the `g_duiDrewForHdc` guard, the image would be drawn dozens of
  times per frame.
- **Per-thread HDC cache.** `g_duiHdc` is `thread_local` because multiple
  Explorer UI threads paint concurrently.
- **Draw only for large bounds.** A 300×200 threshold filters out tiny elements
  and popups.
- **Set the flag only when the bounds are usable.** If the first paint arrives
  before layout settles, the next paint can retry.
- **Call the original first.** The original paints Explorer's own background;
  our image goes on top of that but below the items.

`DrawConfiguredBackground` then:

1. Resolves the current folder (stored per thread from `DocumentComplete`).
2. Loads the cached image and computes the destination rectangle.
3. `SaveDC`, clears the clip region, and intersects it with the view bounds.
4. `AlphaBlend`s the image.
5. Draws the readability scrim.
6. `RestoreDC`.

## 4.5 Clipping and coordinate spaces

All DirectUI coordinates here are **root-relative**, not screen coordinates.
The root element covers the file view. `GetRootRelativeBounds(root)` returns the
area to paint.

The hook explicitly resets the clip region:

```cpp
SelectClipRgn(hdc, nullptr);
IntersectClipRect(hdc, area.left, area.top, area.right, area.bottom);
```

Why? The element being painted may have a small clip rectangle (a single row,
for example). Without resetting it, only a strip of the image would appear.

`SaveDC`/`RestoreDC` ensure that clip and drawing state do not leak back into
Explorer's paint code. Leaking state is a classic cause of subtle corruption.

## 4.6 Empty folders, tabs, and multiple HDCs

Three cases that look similar but behave differently:

- **Empty folder** — no list items paint, but the list element (`ROLE_SYSTEM_LIST`)
  still does. That is why the trigger includes role 33.
- **Tabs** — each tab has its own root element and HDC. The per-thread HDC cache
  handles switching; test tab transitions.
- **Double buffering** — Explorer may paint into a memory DC created with
  `CreateCompatibleDC`. The GDI fallback tracks those; the DirectUI hook keys on
  the actual HDC passed to `PaintBackground`.

When debugging a missing background, log the role and bounds. A bounded probe
is more useful than guessing:

```cpp
if (role == ROLE_SYSTEM_LIST && g_probeCount.fetch_add(1) < 20)
{
    Logging::Info(L"DUI role=33 bounds=" + RectText(&bounds));
}
```

Remove probes before shipping. The production build in this repository logs only
the first 20 actual draws.

---

## Lab 1 — Confirm the roles

Add a temporary probe that logs the first 40 elements whose role is 33 or 34,
including the bounds. Test:

1. an empty folder,
2. a folder with files,
3. the navigation pane,
4. the Recycle Bin.

Which roles and bounds appear? Why does the navigation pane not get the
background? Remove the probe afterwards.

## Lab 2 — Draw a debug border

Temporarily draw a 2-pixel border around `listRect` before drawing the image.
Verify that it aligns with the file view and not the whole window. Then test a
window resize, maximize, and a high-DPI monitor. Remove the border.

## Lab 3 — Break the z-order on purpose

Change the hook to draw **before** calling the original `PaintBackground`.
Rebuild and observe what happens to the image. Explain the result. Restore the
correct order.

## Self-check

<details>
<summary>1. Why does the hook draw only once per HDC?</summary>

Because `PaintBackground` is called for every element and every file item in a
paint pass. Drawing the image for each call would be redundant and expensive.
The `g_duiDrewForHdc` flag ensures one draw per HDC.
</details>

<details>
<summary>2. Why is the DirectUI hook behind the icons while <code>FillRect</code> is above them?</summary>

The DirectUI hook runs inside the list element's paint pass, before items are
painted. The GDI `FillRect` path is a separate layer that Windows 11 composites
above the DirectUI output.
</details>

<details>
<summary>3. Why reset the clip region with <code>SelectClipRgn(hdc, nullptr)</code>?</summary>

The element being painted may have a small clip rectangle. Resetting and then
intersecting with the view bounds allows the full image to be drawn while still
staying inside the view.
</details>

<details>
<summary>4. What should happen if the DirectUI export is renamed?</summary>

`InstallDuiBackground()` returns false, logs the failure, and the project uses
the GDI `FillRect` fallback. No crash, degraded z-order.
</details>

---

## Next

[Chapter 5 — GDI+ images, alpha, and DPI](05-gdi-plus-images.md)
