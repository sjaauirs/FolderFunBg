# Chapter 8 — Packaging and installers

**Learning goals**

- Explain self-contained .NET and Windows App SDK deployment.
- Build a single-file installer with an embedded payload.
- Replace a DLL that Explorer has loaded without breaking the shell.
- Understand elevation manifests, restart races, checksums, and signing.

**Prerequisites:** Chapters 2, 6, and 7. Basic PowerShell.

---

## 8.1 Why packaging is part of the engineering

A working debug build is not a product. Users need:

- an installer that does not require Visual Studio or the .NET SDK,
- a consistent location and a Start Menu shortcut,
- correct COM registration,
- an uninstall path,
- a way to verify the download,
- a predictable behavior when Explorer is running.

The installer is also where many Windows-specific bugs live: file locks,
elevation, anti-virus/SmartScreen, and process restart races.

## 8.2 Self-contained deployment

Two independent runtimes are involved:

1. **.NET** — the C# runtime used by the settings app.
2. **Windows App SDK** — the WinUI 3 runtime.

For an "install on any machine" build:

```xml
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
```

and:

```powershell
dotnet publish ... --self-contained true
```

`WindowsAppSDKSelfContained` copies the Windows App SDK binaries next to the
app. `--self-contained true` includes the .NET runtime. The result is a large
folder (about 226 MB in this project) that runs without installing anything
else.

The trade-off is size versus convenience. A framework-dependent build is much
smaller but requires the .NET 9 runtime and Windows App Runtime on the target
machine. For a learning project or a public preview, self-contained wins.

## 8.3 The installer architecture

`installer/FolderFunBg.Installer` is a small C# console app that:

1. Requests elevation through its manifest.
2. Extracts an embedded `payload.zip` into a temporary folder.
3. Stops Explorer and any running settings app.
4. Copies the hook and the self-contained settings app into
   `%ProgramFiles%\FolderFunBg`.
5. Calls `regsvr32 /s` to register the COM BHO.
6. Creates a Start Menu shortcut using `WScript.Shell`.
7. Restarts Explorer deterministically.
8. Logs everything to `%ProgramData%\FolderFunBg\setup.log`.

The payload is embedded as a managed resource:

```xml
<EmbeddedResource
    Include="$(PayloadZip)"
    LogicalName="FolderFunBg.Payload.zip"
    Condition="Exists('$(PayloadZip)')" />
```

`build-installer.ps1` generates the ZIP and passes its path as the
`PayloadZip` MSBuild property.

### Elevation manifest

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

The process starts already elevated, so every operation runs with administrator
rights. The installer still checks `IsAdministrator()` and fails cleanly if the
manifest is missing or the process is launched in an unusual way.

### Zip-slip protection

Never trust archive entry names. A malicious ZIP can contain `..\..\Windows\...`
paths. The installer resolves each destination and rejects entries outside the
extraction root:

```csharp
var destination = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));
if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidDataException("Payload entry escapes the extraction directory.");
}
```

This is a security boundary, even when the archive is your own build.

## 8.4 Replacing a loaded DLL

This is the hardest part of the installer. `FolderFunBg.Hook.dll` is loaded
inside one or more `explorer.exe` processes. Windows refuses to overwrite a
loaded image:

```text
The process cannot access the file because it is being used by another process.
```

The installer therefore:

1. Stops Explorer.
2. Retries the copy while killing any Explorer instance that Windows
   auto-restarts.
3. Registers the new DLL.
4. Stops Explorer again and starts a fresh instance.

The second stop is important. Windows often auto-restarts the shell a moment
after it is killed. If that auto-restarted instance starts before the copy is
complete, it can load the old DLL and keep it loaded. A deterministic restart
after registration guarantees the final Explorer process loads the new hook.

```csharp
StopExplorerAndWait();
Thread.Sleep(300);
StopExplorerAndWait();   // catch the auto-restart
Process.Start("explorer.exe");
```

The same logic applies to the `install.ps1` and `uninstall.ps1` scripts.

## 8.5 The WinUI PRI gotcha

The self-contained WinUI publish produced an app that crashed at startup with
`0xC000027B` (`STATUS_STOWED_EXCEPTION`). The publish folder was missing:

```text
FolderFunBg.Settings.pri
```

The XAML loader needs the app's merged PRI resource index. The normal build
copies it, but the self-contained publish did not. `build-installer.ps1` now
finds the freshly generated PRI and copies it next to the published EXE:

```powershell
$appPri = Get-ChildItem src\FolderFunBg.Settings\bin -Recurse `
    -Filter FolderFunBg.Settings.pri |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $appPri.FullName (Join-Path $publishDir "FolderFunBg.Settings.pri")
```

This is a real-world lesson: **test the installed artifact, not just the build
output.** The installed app's `--smoke-test` caught the missing resource.

## 8.6 Checksums and code signing

The release publishes:

```text
FolderFunBg-Setup.exe
FolderFunBg-Setup.exe.sha256
```

Users can verify:

```powershell
Get-FileHash .\FolderFunBg-Setup.exe -Algorithm SHA256
```

The preview EXE is **unsigned**, so SmartScreen may warn about an unknown
publisher. Real distribution should use an Authenticode certificate:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f certificate.pfx /p password FolderFunBg-Setup.exe
```

A code-signing certificate is an identity and reputation system, not a
technical guarantee. Keep the private key offline and never commit `.pfx`
files. The repository's `.gitignore` already excludes them.

## 8.7 The build script

`build-installer.ps1`:

1. Builds the native hook with MSBuild.
2. Publishes the settings app self-contained.
3. Copies the generated PRI.
4. Runs the core tests.
5. Stages `Hook/`, `Settings/`, `LICENSE`, and `NOTICE.md`.
6. Creates `payload.zip`.
7. Publishes the installer single-file, embedding the ZIP.
8. Copies `FolderFunBg-Setup.exe` to `install\` and writes its SHA-256.

Why stage a folder before zipping? It keeps the payload layout explicit and
makes it easy to inspect before packaging. Reproducibility and debuggability
matter more than shaving a few seconds.

---

## Lab 1 — Add a repair mode

Add `--repair` to the installer:

- re-copy the hook and app,
- re-register the BHO,
- recreate the shortcut,
- restart Explorer,
- keep the user's config.

Reuse the existing helpers. Test it after deleting the Start Menu shortcut and
renaming the installed hook DLL.

## Lab 2 — Verify the payload

`--verify` already exists. Improve it:

- print the payload file count and total size,
- validate that the hook DLL has a PE header and the expected exports,
- return a non-zero exit code when a file is missing.

Run it from an elevated prompt with `--silent --verify` and inspect the log.

## Lab 3 — Practice safe signing

Create a self-signed code-signing certificate for a lab machine:

```powershell
New-SelfSignedCertificate -Type CodeSigningCert `
    -Subject "CN=FolderFunBg Lab" -CertStoreLocation Cert:\CurrentUser\My
```

Sign the EXE with `signtool`, inspect the signature, and explain why other
machines still do not trust it. Do not distribute lab-signed binaries.

## Lab 4 — Measure and reduce the payload

Measure the size of:

- the hook DLL,
- the settings publish folder,
- `payload.zip`,
- the final setup EXE.

Which files dominate? Try excluding unnecessary runtime components or using a
framework-dependent build, and explain the trade-offs. Do not ship a build you
cannot test on a clean machine.

## Self-check

<details>
<summary>1. Why does the installer stop Explorer twice?</summary>

Windows auto-restarts Explorer after it is killed. The second stop catches that
auto-restarted process so the instance started after registration is guaranteed
to load the new DLL.
</details>

<details>
<summary>2. Why must an archive extractor reject <code>..\</code> paths?</summary>

Otherwise a malicious archive can write outside the extraction directory
(zip-slip). That can overwrite system files or user data.
</details>

<details>
<summary>3. What is the missing-PRI bug?</summary>

The self-contained WinUI publish omitted `FolderFunBg.Settings.pri`, so the
installed app crashed at XAML startup. The build script now copies it.
</details>

<details>
<summary>4. What does a SHA-256 file prove?</summary>

It lets a user verify that the downloaded bytes match the published artifact.
It does not prove who built it; that requires code signing.
</details>

---

## Next

[Chapter 9 — Debugging and observability](09-debugging-and-observability.md)
