# Chapter 2 — COM and the Browser Helper Object

**Learning goals**

- Explain COM interfaces, `IUnknown`, reference counting, and `HRESULT`.
- Describe how Explorer discovers and loads a Browser Helper Object (BHO).
- Read the registration code and the object lifetime in `ComBho.cpp` and
  `dllmain.cpp`.
- Register and unregister the extension safely.

**Prerequisites:** Chapter 1. Basic C++ classes and pointers.

---

## 2.1 COM in one page

COM (Component Object Model) is Windows' language-independent object model. It
predates .NET and is still the backbone of the shell, DirectShow, DirectX, and
many Windows APIs.

A COM object exposes **interfaces**. An interface is a pointer to a table of
function pointers (a vtable), usually written in C++ as:

```cpp
struct IUnknown
{
    virtual HRESULT QueryInterface(REFIID riid, void** ppv) = 0;
    virtual ULONG   AddRef() = 0;
    virtual ULONG   Release() = 0;
};
```

Key rules:

- Every method returns an `HRESULT`. `S_OK` means success; `E_*` values mean
  failure. `FAILED(hr)` checks the sign bit.
- `QueryInterface` asks "do you implement this interface?" It calls `AddRef`
  when it succeeds.
- `AddRef`/`Release` manage lifetime. The object deletes itself when the count
  reaches zero.
- An object can implement several interfaces. The returned pointer is adjusted
  to the correct vtable.
- COM defines a binary ABI, but not the object layout beyond the interface.
  Never assume a specific derived class.

FolderFunBg implements two interfaces:

- `IObjectWithSite` — Explorer gives the object a site pointer via `SetSite`.
- `IDispatch` — the connection point interface used to receive
  `DWebBrowserEvents2` events.

`CIDispatch` implements `IDispatch` and receives events.
`CObjectWithSite` implements `IObjectWithSite` and owns the dispatch object.
Both live in `src/FolderFunBg.Hook/ComBho.cpp`.

## 2.2 How Explorer loads a BHO

A BHO is a COM in-process server registered under a well-known key. The
registration has two parts:

```text
HKEY_CLASSES_ROOT\CLSID\{80BDC533-D192-445A-A1D9-F91310021919}
    \InProcServer32
        (default)      = C:\Program Files\FolderFunBg\FolderFunBg.Hook.dll
        ThreadingModel = Apartment

HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\
    Explorer\Browser Helper Objects\{80BDC533-...}
        NoInternetExplorer = 1
```

When Explorer starts a browser window, it enumerates the BHO key, creates each
CLSID with `CoCreateInstance`, and calls `QueryInterface` for `IObjectWithSite`.
The BHO then calls `SetSite(site)` and hooks the browser's connection point.

`DllRegisterServer` and `DllUnregisterServer` in `ComBho.cpp` write and remove
these keys. `regsvr32.exe` calls those exports.

`DllMain` in `dllmain.cpp` contains a guard:

```cpp
// Simplified: only load inside Explorer or regsvr32.
if (process is not explorer.exe and not regsvr32.exe)
{
    return FALSE;
}
```

This prevents the DLL from being loaded accidentally by other processes. It is
a safety measure, not a security boundary.

## 2.3 The event that supplies the folder

The shell view fires `DWebBrowserEvents2::DocumentComplete` after navigation.
`CIDispatch::Invoke` checks the dispatch ID, extracts the URL from the
`VARIANT` arguments, normalizes it, and stores it per thread.

The argument layout is a classic COM gotcha: `DISPPARAMS::rgvarg` is in
**reverse order** for `IDispatch::Invoke`. The code defensively handles
`VT_BSTR`, `VT_BYREF|VT_BSTR`, and `VT_BYREF|VT_VARIANT` because different hosts
have used different representations.

It also deduplicates consecutive identical paths:

```cpp
if (location == m_lastPath) return S_OK;
```

This avoids redundant work when Explorer re-navigates to the same folder.

## 2.4 Reference counting in practice

COM lifetime bugs are common. Two rules keep this code sane:

1. `QueryInterface` always calls `AddRef` before returning a successful pointer.
2. `Release` deletes the object exactly when the count reaches zero, and no
   method touches members after that.

The project also tracks global object and lock counts for diagnostics. A BHO
that never releases its site can keep Explorer objects alive; a BHO that
releases too early crashes. When debugging, watch the log for lifecycle
messages and use Process Explorer to confirm the DLL is loaded.

## 2.5 Apartment threading

Explorer creates the BHO on a UI thread in a single-threaded apartment (STA).
The hook callbacks therefore run on UI threads. FolderFunBg avoids blocking
those threads:

- config loading happens on a watcher thread,
- the config pointer is swapped under a shared mutex,
- drawing uses a per-image mutex,
- no COM calls are made from the paint hook.

Never call `CoInitialize`/`CoUninitialize` inside a detour that may run on an
unexpected thread. Keep paint-path code allocation-light and lock-light.

---

## Lab 1 — Inspect the registration

1. Open `regedit` as administrator.
2. Navigate to the two keys shown above.
3. Note the `InProcServer32` path and `ThreadingModel`.
4. Close `regedit`.

Then unregister and re-register from an elevated prompt:

```powershell
regsvr32 /s "C:\Program Files\FolderFunBg\FolderFunBg.Hook.dll"
regsvr32 /s /u "C:\Program Files\FolderFunBg\FolderFunBg.Hook.dll"
```

Check the keys after each command. Remember to re-register, or use the setup
EXE, before continuing with later labs.

## Lab 2 — Read the object lifecycle

Open `src/FolderFunBg.Hook/ComBho.cpp` and answer:

1. Which interfaces does `CIDispatch::QueryInterface` accept?
2. Where is `AppState::Start()` called?
3. Where is the site pointer stored, and when is it released?
4. What happens if `DocumentComplete` fires with no URL?

Then add a bounded log line to `SetSite`:

```cpp
Logging::Info(L"BHO site set.");
```

Rebuild, restart Explorer, and confirm the line appears once per Explorer
process, not once per window. Remove the line afterwards.

## Lab 3 — A minimal BHO outline

Write a design note (not code) for a BHO that only logs navigation events:

- Which interfaces must it implement?
- Which registry keys must it write?
- Which thread does `DocumentComplete` arrive on?
- How will it avoid logging after the object is destroyed?

Compare your answer with `ComBho.cpp`. The real code is intentionally compact;
your design should identify the same lifetime hazards.

## Self-check

<details>
<summary>1. Why does <code>QueryInterface</code> call <code>AddRef</code>?</summary>

Because a successful interface pointer is a new reference. The caller will call
`Release` on it. Without `AddRef`, the object could be destroyed while the
caller still holds the pointer.
</details>

<details>
<summary>2. Why is <code>rgvarg</code> reversed?</summary>

COM's `DISPPARAMS` stores arguments in reverse order (the last argument is at
index 0). The code reads the URL from `rgvarg[0]` accordingly.
</details>

<details>
<summary>3. What is the difference between <code>CObjectWithSite</code> and <code>CIDispatch</code>?</summary>

`CObjectWithSite` is the object Explorer creates; it implements
`IObjectWithSite` and receives the site. `CIDispatch` implements `IDispatch`
and receives the browser events. They cooperate but expose different
interfaces.
</details>

<details>
<summary>4. Why can't the paint hook call COM?</summary>

It runs synchronously inside Explorer's rendering path on a UI thread. COM calls
can block, re-enter the shell, or require an initialized apartment. The paint
path must stay deterministic and fast.
</details>

---

## Next

[Chapter 3 — Function hooking with MinHook](03-hooking-with-minhook.md)
