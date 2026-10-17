# Further reading

Official documentation, tools, and books for going deeper. Links are grouped by
topic and point to Microsoft Learn unless noted.

## Windows shell and Win32

- [Windows shell documentation](https://learn.microsoft.com/windows/win32/shell/shell-entry)
- [Windows and Messages](https://learn.microsoft.com/windows/win32/winmsg/windows)
- [Window classes](https://learn.microsoft.com/windows/win32/winmsg/window-classes)
- [Process Explorer](https://learn.microsoft.com/sysinternals/downloads/process-explorer)
- [Windows Internals, 7th edition (Russinovich, Ionescu, Solomon)](https://learn.microsoft.com/sysinternals/resources/windows-internals)
- *Programming Windows*, 5th edition, Charles Petzold — old but unmatched for
  learning the Win32 message model.

## COM and shell extensions

- [Component Object Model (COM)](https://learn.microsoft.com/windows/win32/com/component-object-model--com--portal)
- [`IObjectWithSite`](https://learn.microsoft.com/windows/win32/api/ocidl/nn-ocidl-iobjectwithsite)
- [`DWebBrowserEvents2`](https://learn.microsoft.com/windows/win32/api/exdisp/nn-exdisp-dwebbrowserevents2)
- [Shell extensions](https://learn.microsoft.com/windows/win32/shell/shell-exts)
- [Registering a COM server](https://learn.microsoft.com/windows/win32/com/registering-a-com-server)

## Function hooking

- [MinHook (GitHub)](https://github.com/TsudaKageyu/minhook)
- [Detours (Microsoft Research)](https://github.com/microsoft/Detours)
- [x64 calling convention](https://learn.microsoft.com/cpp/build/x64-calling-convention)
- [Exception handling for x64](https://learn.microsoft.com/cpp/build/exception-handling-x64)

DirectUI is not officially documented. The hook in this project is research on a
specific Windows build. Treat any article or snippet that names a `dui70.dll`
export as version-specific.

## Graphics

- [GDI+ documentation](https://learn.microsoft.com/windows/win32/gdiplus/-gdiplus-gdi-start)
- [`AlphaBlend`](https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-alphablend)
- [Device contexts](https://learn.microsoft.com/windows/win32/gdi/device-contexts)
- [High DPI desktop application development](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)

## WinUI 3 and Windows App SDK

- [WinUI 3 documentation](https://learn.microsoft.com/windows/apps/winui/winui3/)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [Deploy unpackaged apps](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps)
- [Self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [Windows App SDK deployment overview](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-overview)

## .NET and configuration

- [.NET publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)
- [Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/overview)
- [`File.Move` and atomic writes](https://learn.microsoft.com/dotnet/api/system.io.file.move)

## Installers and signing

- [SignTool](https://learn.microsoft.com/windows/win32/seccrypto/signtool)
- [Authenticode code signing](https://learn.microsoft.com/windows-hardware/drivers/install/authenticode)
- [SmartScreen reputation](https://learn.microsoft.com/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/)
- [WiX Toolset](https://wixtoolset.org/) — if you outgrow a custom installer
- [Inno Setup](https://jrsoftware.org/isinfo.php) — a popular scripted installer

## Debugging and diagnostics

- [WinDbg](https://learn.microsoft.com/windows-hardware/drivers/debugger/)
- [Windows Error Reporting](https://learn.microsoft.com/windows/win32/wer/windows-error-reporting)
- [Event Tracing for Windows](https://learn.microsoft.com/windows/win32/etw/event-tracing-portal)
- [Process Monitor](https://learn.microsoft.com/sysinternals/downloads/procmon)
- [Accessibility Insights for Windows](https://accessibilityinsights.io/)

## Security and ethics

- [Secure coding guidelines](https://learn.microsoft.com/windows/win32/secbp/secure-coding-guidelines)
- [Microsoft bug bounty and coordinated disclosure](https://www.microsoft.com/msrc/bounty)
- [Responsible disclosure](https://www.microsoft.com/msrc/cvd)

## Similar and related projects

- [Maplespe/explorerTool](https://github.com/Maplespe/explorerTool) — an MIT
  project that uses a similar BHO + MinHook technique. Compare the designs.
- [ExplorerPatcher](https://github.com/valinet/ExplorerPatcher) — a large
  open-source Explorer modification project; excellent reading on compatibility
  and maintenance burden.
- [Daniel Marshall's COM and shell notes](https://github.com/danielmarschall)
  — practical Windows shell examples.

## How to read these sources

1. Start with the official page for the API you are using.
2. Read the *remarks* and *requirements* sections; they contain the constraints
   tutorials usually omit.
3. Find one open-source project that uses the API and compare its error
   handling with yours.
4. Write a tiny reproduction before integrating it into a larger codebase.

---

Back to the [learning index](README.md).
