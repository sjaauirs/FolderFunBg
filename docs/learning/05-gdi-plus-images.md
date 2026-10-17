# Chapter 5 — GDI+ images, alpha, and DPI

**Learning goals**

- Explain the relationship between GDI, GDI+, an HDC, and a memory DC.
- Load and validate an image safely inside Explorer.
- Implement the position modes with integer math.
- Draw with alpha blending and a readability scrim without corrupting the DC.
- Reason about DPI and performance.

**Prerequisites:** Chapter 4. Basic 2D geometry.

---

## 5.1 GDI and GDI+

Windows has two related 2D APIs:

- **GDI** (Graphics Device Interface) — the original Win32 drawing API. It works
  with `HDC` (device context) handles, pens, brushes, and bitmaps.
- **GDI+** — a later object-oriented wrapper. It adds anti-aliasing, image
  formats, gradients, and better alpha support.

FolderFunBg uses both:

- GDI for the destination `HDC` and the `AlphaBlend` call,
- GDI+ for loading PNG/JPEG files and for gradients.

An **HDC** is a handle to a drawing state: a target surface, a clip region, a
coordinate transform, selected objects, and colors. `SaveDC(hdc)` pushes that
state; `RestoreDC(hdc, saved)` pops it. Always restore a DC you did not create.

## 5.2 Loading an image without crashing Explorer

The image cache (`src/FolderFunBg.Hook/ImageCache.cpp`) does four things:

1. Opens the file and reads the header bytes to validate the PNG/JPEG
   signature.
2. Creates a GDI+ `Bitmap` from an `IStream` over the file bytes.
3. Converts it to an HBITMAP-compatible memory DC for fast `AlphaBlend`.
4. Samples an 8×8 grid to compute average luminance.

Why validate the signature? A malformed image can crash GDI+ inside Explorer,
taking the whole shell down. Rejecting bad files at the boundary is cheaper and
safer than hoping the decoder handles them.

Why an `IStream` instead of `Bitmap(path)`? It avoids keeping a file handle open
and gives the cache control over the bytes and lifetime.

The cache is a map from path to `shared_ptr<CachedImage>`. A failed set prevents
retrying a broken file on every paint.

```cpp
std::shared_ptr<CachedImage> ImageCache::Get(const std::wstring& path)
{
    // lock, look up, load on miss, remember failures
}
```

`CachedImage::Draw` holds a per-image mutex because two Explorer threads can
paint the same image simultaneously. The GDI+ object itself is not designed for
concurrent drawing from multiple threads.

## 5.3 Alpha blending

The workhorse is:

```cpp
AlphaBlend(
    destinationDc, dx, dy, dw, dh,
    sourceDc, 0, 0, sourceWidth, sourceHeight,
    blendFunction);
```

`BLENDFUNCTION`:

```cpp
blend.BlendOp = AC_SRC_OVER;
blend.SourceConstantAlpha = alpha;   // 0-255
blend.AlphaFormat = AC_SRC_ALPHA;    // respect the image's alpha channel
```

- `SourceConstantAlpha` scales the whole image.
- `AC_SRC_ALPHA` respects per-pixel transparency (for PNG images with alpha).

If the image has no alpha channel, `AC_SRC_ALPHA` still works because GDI+
produces an opaque bitmap.

## 5.4 Position math

Let the view be `areaWidth × areaHeight` and the image be `imageWidth ×
imageHeight`. The destination rectangle is `(x, y, width, height)`.

| Mode | x | y | width | height |
|---|---|---|---|---|
| top-left | 0 | 0 | image | image |
| top-right | `areaWidth - imageWidth` | 0 | image | image |
| bottom-left | 0 | `areaHeight - imageHeight` | image | image |
| bottom-right | `areaWidth - imageWidth` | `areaHeight - imageHeight` | image | image |
| center | `(areaWidth - imageWidth) / 2` | `(areaHeight - imageHeight) / 2` | image | image |
| stretch | 0 | 0 | `areaWidth` | `areaHeight` |
| zoom-fill | `(areaWidth - width) / 2` | `(areaHeight - height) / 2` | scaled | scaled |

**Zoom-fill** preserves the aspect ratio and fills the view, cropping the
overflow:

```text
scale = max(areaWidth / imageWidth, areaHeight / imageHeight)
width  = round(imageWidth * scale)
height = round(imageHeight * scale)
```

`stretch` distorts when the aspect ratios differ. `zoom-fill` never shows empty
space but may crop. A future `fit` mode would use `min(...)` and letterbox.

All values are integers. Division truncates toward zero; for negative offsets the
rounding can differ by a pixel. If you care about symmetry, use `std::lround` on
a floating-point calculation, as the zoom-fill code does.

## 5.5 The readability scrim

The scrim is a translucent layer drawn over the image to improve text contrast.
`DrawReadabilityScrim` supports:

- `none` — no scrim,
- `light` — white scrim,
- `dark` — black scrim,
- `auto` — choose from the image luminance and the Windows theme.

Auto logic in light theme:

```text
if luminance >= 0.45: no scrim (the image is already light enough)
else: white scrim, alpha scaled by (1 - luminance / 0.45)
```

In dark theme it is mirrored: a light image gets a black scrim, and a dark image
needs none. The gradient variant is strongest on the left, where file names
usually are, and fades to transparent on the right.

`strength` is clamped to 0–255. A strength of 0 disables the scrim.

## 5.6 DPI and scaling

A DirectUI `HDC` uses physical pixels for the monitor the window is on. The
image is drawn at its natural pixel size unless a mode scales it. On a 200%
monitor, a 400-pixel image covers half as much logical space as on a 100%
monitor.

Possible improvements:

- scale the image by the monitor DPI (`GetDpiForWindow`),
- cache a DPI-scaled variant per monitor,
- use `SetThreadDpiAwarenessContext` if you call APIs from a mixed-DPI thread.

FolderFunBg currently draws 1:1. Document the behavior rather than pretending it
is DPI-perfect. Testing at 100%, 150%, and 200% is part of the manual matrix.

## 5.7 Performance rules

- Cache decoded images; never decode a PNG in a paint pass.
- Draw once per HDC (Chapter 4).
- Keep `Draw` under a per-image lock only for the `AlphaBlend` call.
- Clear the cache when the config changes so deleted/replaced images are not
  reused.
- Log only bounded events.

A 4K image scaled into a small rectangle costs more than a pre-sized image. For
a production tool, generate and cache a scaled bitmap per target size.

---

## Lab 1 — Add a `fit` position

Implement a `fit` mode that preserves the aspect ratio and letterboxes inside
the view:

```text
scale = min(areaWidth / imageWidth, areaHeight / imageHeight)
width  = round(imageWidth * scale)
height = round(imageHeight * scale)
x = (areaWidth - width) / 2
y = (areaHeight - height) / 2
```

You must update:

1. the C++ `Position` enum and parser,
2. the C# `BackgroundPositions` list,
3. the XAML ComboBox items (they bind to the list),
4. tests and documentation.

This is the classic "vertical slice" exercise: one feature, every layer.

## Lab 2 — Draw a debug frame

Draw a 2-pixel rectangle around the image's destination rectangle with a bright
color. Verify each position mode, including zoom-fill cropping. Remove the
frame.

## Lab 3 — Profile a large image

Create a 6000×4000 PNG and a 800×600 version of the same picture. Measure the
paint time (bounded `QueryPerformanceCounter`) and memory use for each. Explain
the difference. Then implement a simple downscale-on-load rule for images larger
than 2× the view size.

## Self-check

<details>
<summary>1. Why use <code>SaveDC</code>/<code>RestoreDC</code>?</summary>

To prevent clip regions, selected objects, and colors from leaking into
Explorer's paint code. A leaked clip region can make later UI disappear.
</details>

<details>
<summary>2. What is the difference between stretch and zoom-fill?</summary>

Stretch maps the image to the full rectangle, distorting the aspect ratio.
Zoom-fill scales uniformly to cover the rectangle, preserving aspect ratio and
cropping the overflow.
</details>

<details>
<summary>3. Why sample the image luminance instead of checking the average color?</summary>

Luminance approximates perceived brightness and is cheap to compute from RGB.
Average RGB can mislead for saturated colors. The auto scrim only needs a rough
classification.
</details>

<details>
<summary>4. Why validate the image signature before decoding?</summary>

A malformed image can crash the GDI+ decoder inside Explorer. Rejecting it at
the boundary protects the shell.
</details>

---

## Next

[Chapter 6 — Configuration, rules, and hot reload](06-config-and-hot-reload.md)
