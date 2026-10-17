# Chapter 3 — Function hooking with MinHook

**Learning goals**

- Explain what a function detour is and what a trampoline does.
- Describe the x64 calling convention well enough to read the hook signatures.
- Install, enable, and remove hooks safely.
- Avoid the classic hooking failures: recursion, stack imbalance, exceptions
  crossing the boundary, and unloading with live threads.

**Prerequisites:** Chapters 1–2. Comfort with pointers and the stack.

---

## 3.1 What is a detour?

A caller normally executes the first instructions of a function, then continues
at the function body. A **detour** (also called a hook or patch) replaces those
first bytes with a jump to your function.

```text
caller ──▶ [patched prologue: jmp Hook] ──▶ Hook()
                                             │
                                             ├─ do extra work
                                             └─ call trampoline
                                                   │
                                                   ▼
                                           original function body
```

The **trampoline** is a copy of the overwritten instructions followed by a jump
back into the original function. It lets the hook call the original function
without recursively hitting its own patch.

This is not limited to export tables. It works on any function whose first
instructions can be relocated. MinHook handles relocation, allocation of the
trampoline, and thread safety of the patch.

Some systems provide supported alternatives:

- **Detours** (Microsoft Research) — similar idea, different implementation.
- **IAT/EAT hooking** — replace an import/export table pointer; only works for
  indirect calls through that table.
- **VTable hooking** — replace a virtual method pointer; requires an object or
  a vtable symbol.
- **Instrumentation callbacks** — supported for some subsystems, not for
  Explorer's painting path.

FolderFunBg uses MinHook because it is small, MIT licensed, and works on
private internal functions such as `DirectUI::Element::PaintBackground`.

## 3.2 The x64 calling convention

On Windows x64, the first four integer/pointer arguments are passed in
`RCX`, `RDX`, `R8`, `R9`; floating-point arguments use `XMM0`–`XMM3`. The caller
reserves 32 bytes of **shadow space** on the stack. The callee must preserve
`RBX`, `RBP`, `RDI`, `RSI`, `RSP`, and `R12`–`R15`.

That is why the hook signatures use `__fastcall` explicitly (in practice it is
the same as the standard x64 convention) and match the original argument types
exactly:

```cpp
using PaintBackground_t =
    void(__fastcall*)(void*, HDC, void*, const RECT*,
                      const RECT*, const RECT*, const RECT*);
```

If you get the argument list wrong, you will corrupt the stack or pass garbage
pointers. When hooking an undocumented function, verify the signature with a
debugger and a small probe before trusting it.

## 3.3 MinHook lifecycle

```cpp
MH_Initialize();                          // once per process
MH_CreateHook(target, hook, &original);   // prepare a detour
MH_EnableHook(MH_ALL_HOOKS);              // activate
...
MH_DisableHook(MH_ALL_HOOKS);
MH_Uninitialize();
```

In FolderFunBg, `Hooks::Install()` does this in order:

1. Initialize MinHook.
2. Create detours for `CreateWindowExW`, `DestroyWindow`, `BeginPaint`,
   `FillRect`, and `CreateCompatibleDC`.
3. Try to install the DirectUI `PaintBackground` hook.
4. Enable all hooks.
5. Log success or failure.

If creation or enabling fails, it uninitializes MinHook and reports failure.
The BHO retries on navigation via `AppState::EnsureHooks()`.

## 3.4 The hooks in this project

| Hook | Why it exists |
|---|---|
| `CreateWindowExW` | Detect a new `DirectUIHWND` whose parent chain is a folder view |
| `DestroyWindow` | Stop tracking a closed view |
| `BeginPaint` | Remember the HDC associated with a tracked window |
| `CreateCompatibleDC` | Remember double-buffer DCs used by the shell |
| `FillRect` | Fallback: draw after the GDI background erase |
| `PaintBackground` | Primary: draw inside DirectUI's paint pass |

`CreateWindowExW` runs on the thread creating the window. `BeginPaint` runs on
the painting thread. `PaintBackground` runs during DirectUI rendering. The hook
state is therefore both per-thread and per-HWND.

### Tracking

`g_windows` maps a thread ID to a map of `HWND` → `WindowState` (which stores the
current HDC). `g_locations` maps a thread ID to the folder path. Access is
guarded by `g_stateMutex`.

This is not a general-purpose window database. It only tracks the specific
`DirectUIHWND` instances that belong to a folder view, which keeps the paint
path cheap.

## 3.5 Hook safety rules

1. **Never let an exception escape.** Every hook body is wrapped in
   `try/catch (...)`. An exception crossing a C boundary into Explorer's frame
   is undefined behavior.
2. **Always call the original.** Unless you intentionally swallow a call, call
   the trampoline first or last according to the desired behavior.
3. **Do not log on every paint.** Use a bounded counter. A per-pixel or
   per-frame log will destroy performance and fill the disk.
4. **Avoid blocking.** No file I/O, network, `Sleep`, or COM calls in the paint
   path.
5. **Respect the callback thread.** Hooks run on whatever thread Explorer uses.
   Do not assume the UI thread or an initialized COM apartment.
6. **Uninstall before unloading.** `Uninstall()` disables hooks before MinHook is
   uninitialized. If another thread is inside a hook when the DLL unloads, the
   process will crash. Explorer normally keeps the DLL loaded for its lifetime.

## 3.6 The fallback strategy

The DirectUI export is version-sensitive. If it is missing:

```cpp
g_duiAvailable = InstallDuiBackground();
```

is false, and `HookPaintRect` becomes the active renderer:

```cpp
int WINAPI HookFillRect(HDC hdc, const RECT* rect, HBRUSH brush)
{
    const int result = g_originalFillRect(hdc, rect, brush);
    if (!g_duiAvailable.load())
    {
        DrawBackground(hdc, rect);
    }
    return result;
}
```

This means the product still works on a build where the DirectUI symbol is
renamed, but with the older above-the-items z-order. Good engineering with
undocumented APIs always has a degraded mode.

---

## Lab 1 — Count window creation

Add a bounded counter to `HookCreateWindowExW` that logs the first 40
`DirectUIHWND` creations with their parent class name. Rebuild, install, and
restart Explorer. Open and close several windows and tabs. Answer:

- How many `DirectUIHWND` windows does one Explorer window create?
- Does the count change when you open a new tab?
- Are any `DirectUIHWND` windows created outside a folder view?

Remove the log line when you are done.

## Lab 2 — Measure the hook cost

Add a `QueryPerformanceCounter` measurement around the body of
`HookPaintBackground` (only for a bounded number of calls). Log the average in
microseconds after 100 calls. Compare:

- an empty folder,
- a folder with 200 items,
- a folder with the background disabled.

What dominates the cost: your hook, DirectUI, or file enumeration? Do not keep
the instrumentation in the final build.

## Lab 3 — Read the install failure paths

In `Hooks::Install()`:

1. What happens if `MH_CreateHook` fails halfway through the chain?
2. What happens if `MH_EnableHook` fails?
3. Why does the code call `MH_Uninitialize()` in that case?
4. How does `AppState::EnsureHooks()` recover?

Write a short paragraph explaining the failure behavior. This is the kind of
question an interviewer may ask about production hooking code.

## Self-check

<details>
<summary>1. Why is a trampoline needed?</summary>

The patched prologue no longer contains the original instructions. The
trampoline holds a relocated copy plus a jump back into the function, so the
hook can call the original without hitting its own jump.
</details>

<details>
<summary>2. Why must the hook signature match exactly?</summary>

The caller and callee agree on registers and stack layout. A wrong parameter
type or count reads the wrong registers/stack, causing garbage pointers or stack
corruption.
</details>

<details>
<summary>3. Why is logging every <code>PaintBackground</code> call dangerous?</summary>

DirectUI repaints frequently (hover, selection, scrolling, resize). Logging each
call causes heavy file I/O in the render path and can make Explorer unusable.
Bounded counters are the safe pattern.
</details>

<details>
<summary>4. What is the purpose of the GDI fallback?</summary>

If the DirectUI export is unavailable or the hook cannot install, `FillRect`
still draws the background. The z-order is worse (above item text), but the
feature survives.
</details>

---

## Next

[Chapter 4 — DirectUI and the painting path](04-directui-and-painting.md)
