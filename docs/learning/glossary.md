# Glossary

Terms used in the project and the course. Definitions are intentionally short;
follow the links in [further reading](further-reading.md) for depth.

## Windows and shell

**HWND** — Handle to a window. Windows owns the object; your process holds an
opaque handle.

**Window class** — A registered name that defines a window's procedure and
default behavior, such as `CabinetWClass` or `DirectUIHWND`.

**Shell view** — The component that displays a folder's contents. In Explorer it
is hosted by `SHELLDLL_DefView`.

**`SHELLDLL_DefView`** — The window class of the shell view host.

**`DirectUIHWND`** — A Win32 host window for a DirectUI element tree. Modern
Explorer's file list lives here.

**Shell namespace** — The logical tree of folders, including virtual folders
such as This PC and the Recycle Bin.

**CLSID** — A GUID that identifies a COM class or a shell namespace folder.

**Desktop.ini** — A per-folder configuration file historically used for icons,
tooltips, and `IconArea_Image`. `IconArea_Image` is not honored on current
Windows 11 builds.

## COM

**COM** — Component Object Model. A binary object model based on interfaces,
reference counting, and `HRESULT`.

**Interface** — A named set of virtual methods with a defined binary layout.

**`IUnknown`** — The root COM interface: `QueryInterface`, `AddRef`, `Release`.

**`IDispatch`** — Late-bound COM interface used for automation and event sinks.

**`IObjectWithSite`** — Interface that lets a host pass a site pointer to a
plugin.

**`DWebBrowserEvents2`** — The browser event interface. Explorer's shell view
fires `DocumentComplete` through it.

**BHO** — Browser Helper Object. A COM in-process server registered for
Explorer, loaded into its process.

**STA** — Single-threaded apartment. COM objects that are not thread-safe live
in an STA; calls are marshalled to its thread.

**`HRESULT`** — A 32-bit result code. `S_OK` is success; `E_*` values are
failures.

**Reference counting** — `AddRef` increments, `Release` decrements, and the
object deletes itself at zero.

## Hooking

**Detour / hook** — Replacing the start of a function with a jump to your code.

**Trampoline** — A relocated copy of the overwritten instructions plus a jump
back to the original function.

**MinHook** — A small MIT-licensed x64 detour library used by this project.

**Reentrancy** — A hook calling its target again, directly or indirectly.
Recursion can crash if not handled.

**Bounded logging** — Logging only the first N events to keep a render path cheap.

**Fallback** — A second implementation used when the primary one cannot install.
Here, GDI `FillRect` is the fallback for the DirectUI hook.

## DirectUI

**DirectUI** — Microsoft's internal retained-mode UI toolkit in `dui70.dll`.

**Element** — A node in a DirectUI tree. Elements have parents, children,
bounds, and roles.

**Role** — An accessibility role such as `ROLE_SYSTEM_LIST` (33) or
`ROLE_SYSTEM_LISTITEM` (34), from the MSAA constants.

**`PaintBackground`** — An internal `DirectUI::Element` method that paints a
background. FolderFunBg detours it.

**Decorated name** — A C++ mangled symbol such as
`?PaintBackground@Element@DirectUI@@...`. It encodes the class, method, and
signature.

**Z-order** — The painting order of visual layers. Drawing after the list
background but before items puts the image behind the items.

## Graphics

**GDI** — Graphics Device Interface. The original Win32 2D drawing API.

**GDI+** — A later object-oriented 2D API with image codecs, gradients, and
alpha blending.

**HDC** — Handle to a device context: the drawing target and its state.

**Memory DC** — A device context backed by a bitmap rather than a screen, used
for off-screen composition.

**Clipping region** — The part of the surface drawing is allowed to touch.

**`AlphaBlend`** — A GDI function that composites a source bitmap with alpha.

**`BLENDFUNCTION`** — The structure that controls `AlphaBlend` behavior.

**Scrim** — A translucent layer that improves text contrast over an image.

**Luminance** — Perceived brightness, used by the `auto` scrim mode.

**DPI** — Dots per inch. A 200% monitor has twice the pixel density; physical
pixel sizes differ from logical sizes.

## Packaging and deployment

**Self-contained** — The app carries its runtime dependencies and does not need
them installed system-wide.

**Windows App SDK** — The runtime for WinUI 3 and related APIs.

**Unpackaged app** — A desktop app without MSIX package identity.

**PRI** — Package Resource Index. A compiled resource index used by XAML.

**Single-file publish** — A .NET publish mode that bundles the app and runtime
into one executable.

**Embedded payload** — Application files stored inside the installer executable
as a resource, extracted at install time.

**Elevation** — Running with administrator rights, usually requested by a
manifest and approved through UAC.

**UAC** — User Account Control, the Windows elevation prompt.

**Zip-slip** — An archive attack where entry names escape the extraction
directory using `..\`.

**SHA-256** — A cryptographic hash used to verify file integrity.

**Code signing** — Using an Authenticode certificate to establish the publisher
of a binary.

**SmartScreen** — Windows' reputation-based warning system for downloaded
executables.

**WER** — Windows Error Reporting. Stores crash reports and minidumps.

**PDB** — Program database. Debug symbols for a native or managed binary.

**ETW** — Event Tracing for Windows. A low-overhead tracing facility.

**MSIX** — The modern Windows app packaging format. Not used by this project.

---

Back to the [learning index](README.md).
