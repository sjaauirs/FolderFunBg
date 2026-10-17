using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace FolderFunBg.Installer;

internal static class Program
{
    private const string ProductName = "FolderFunBg";
    private const string HookFileName = "FolderFunBg.Hook.dll";
    private const string Clsid = "{80BDC533-D192-445A-A1D9-F91310021919}";
    private const string LegacyClsid = "{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}";
    private const uint MbIconInformation = 0x40;
    private const uint MbIconError = 0x10;

    private static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), ProductName);

    private static readonly string HookPath = Path.Combine(InstallDir, HookFileName);
    private static readonly string SettingsDir = Path.Combine(InstallDir, "Settings");
    private static readonly string SettingsExe = Path.Combine(SettingsDir, "FolderFunBg.Settings.exe");

    private static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        ProductName, "FolderFunBg Settings.lnk");

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductName);

    private static readonly string LogPath = Path.Combine(LogDir, "setup.log");

    private static bool _silent;

    [STAThread]
    private static int Main(string[] args)
    {
        _silent = args.Any(argument => argument.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        try
        {
            Log($"Setup started. Version={typeof(Program).Assembly.GetName().Version}. Args={string.Join(' ', args)}");

            if (!IsAdministrator())
            {
                throw new InvalidOperationException("Administrator rights are required. Run this installer again and approve the UAC prompt.");
            }

            if (args.Any(argument => argument.Equals("--verify", StringComparison.OrdinalIgnoreCase)))
            {
                return VerifyPayload();
            }

            if (args.Any(argument => argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Uninstall();
                return 0;
            }

            Install();
            return 0;
        }
        catch (Exception exception)
        {
            Log("FATAL: " + exception);
            Notify(
                "FolderFunBg setup failed:\n\n" + exception.Message +
                "\n\nA log was written to:\n" + LogPath,
                "FolderFunBg Setup",
                MbIconError);
            return 1;
        }
    }

    private static void Install()
    {
        EnsureNoLegacyBho();

        var extractDir = ExtractPayload();
        try
        {
            var sourceHook = Path.Combine(extractDir, "Hook", HookFileName);
            var sourceSettings = Path.Combine(extractDir, "Settings");

            if (!File.Exists(sourceHook))
            {
                throw new FileNotFoundException("The installer payload does not contain the native hook.", sourceHook);
            }

            if (!Directory.Exists(sourceSettings) || !File.Exists(Path.Combine(sourceSettings, "FolderFunBg.Settings.exe")))
            {
                throw new DirectoryNotFoundException("The installer payload does not contain the settings app.");
            }

            // Stop Explorer before touching the hook: a loaded DLL cannot be overwritten.
            StopProcesses("explorer", "FolderFunBg.Settings");
            Directory.CreateDirectory(InstallDir);

            CopyFileWithRetry(sourceHook, HookPath);
            CopyDirectoryWithRetry(sourceSettings, SettingsDir);
            RunRegsvr32($"/s \"{HookPath}\"");
            CreateShortcut();
            RestartExplorer();

            Log($"Install complete. Hook={HookPath} Settings={SettingsExe}");
            Notify(
                "FolderFunBg was installed successfully.\n\n" +
                "Open \"FolderFunBg Settings\" from the Start Menu to choose an image.\n\n" +
                "Installed to:\n" + InstallDir,
                "FolderFunBg Setup",
                MbIconInformation);
        }
        finally
        {
            StartExplorer();
            TryDeleteDirectory(extractDir);
        }
    }

    private static void Uninstall()
    {
        StopProcesses("explorer", "FolderFunBg.Settings");

        if (File.Exists(HookPath))
        {
            try
            {
                RunRegsvr32($"/s /u \"{HookPath}\"");
            }
            catch (Exception exception)
            {
                // Keep going: the registry cleanup below still removes the registration.
                Log("regsvr32 /u warning: " + exception.Message);
            }
        }

        DeleteRegistryKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{Clsid}");
        DeleteRegistryKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{LegacyClsid}");
        DeleteRegistryKey($@"SOFTWARE\Classes\CLSID\{Clsid}");

        if (File.Exists(ShortcutPath))
        {
            try { File.Delete(ShortcutPath); }
            catch (Exception exception) { Log("Shortcut delete warning: " + exception.Message); }
        }

        var shortcutDir = Path.GetDirectoryName(ShortcutPath);
        if (shortcutDir is not null && Directory.Exists(shortcutDir) && !Directory.EnumerateFileSystemEntries(shortcutDir).Any())
        {
            try { Directory.Delete(shortcutDir); } catch { }
        }

        DeleteDirectoryWithRetry(InstallDir);
        RestartExplorer();

        Log("Uninstall complete. User config in %LOCALAPPDATA% was kept.");
        Notify(
            "FolderFunBg was removed.\n\n" +
            "Your per-user configuration and images were kept in:\n" +
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName),
            "FolderFunBg Setup",
            MbIconInformation);
    }

    private static int VerifyPayload()
    {
        var extractDir = ExtractPayload();
        try
        {
            var hook = Path.Combine(extractDir, "Hook", HookFileName);
            var settings = Path.Combine(extractDir, "Settings", "FolderFunBg.Settings.exe");
            var hookOk = File.Exists(hook);
            var settingsOk = File.Exists(settings);
            var ok = hookOk && settingsOk;

            var detail =
                $"Hook: {(hookOk ? new FileInfo(hook).Length + " bytes" : "MISSING")}\n" +
                $"Settings app: {(settingsOk ? "present" : "MISSING")}";

            Log("Verify payload: " + detail.Replace('\n', ' '));
            Notify(
                (ok ? "Installer payload is valid.\n\n" : "Installer payload is INVALID.\n\n") + detail,
                "FolderFunBg Setup",
                ok ? MbIconInformation : MbIconError);
            return ok ? 0 : 3;
        }
        finally
        {
            TryDeleteDirectory(extractDir);
        }
    }

    private static string ExtractPayload()
    {
        var extractDir = Path.Combine(Path.GetTempPath(), "FolderFunBg-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDir);

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FolderFunBg.Payload.zip")
            ?? throw new InvalidOperationException(
                "The installer payload is missing. Build the installer with build-installer.ps1.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var root = Path.GetFullPath(extractDir) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Payload entry escapes the extraction directory: " + entry.FullName);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        Log($"Payload extracted to {extractDir}");
        return extractDir;
    }

    private static void EnsureNoLegacyBho()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{LegacyClsid}");

        if (key is not null)
        {
            throw new InvalidOperationException(
                "ExplorerBgTool is still registered. Uninstall it before installing FolderFunBg; " +
                "two tools that hook the same drawing path can crash Explorer.");
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RunRegsvr32(string arguments)
    {
        var regsvr32 = Path.Combine(Environment.SystemDirectory, "regsvr32.exe");
        using var process = Process.Start(new ProcessStartInfo(regsvr32, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start regsvr32.exe.");

        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("regsvr32.exe did not finish within 30 seconds.");
        }

        var error = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"regsvr32.exe failed with exit code {process.ExitCode}. {error}".Trim());
        }

        Log("regsvr32 " + arguments + " succeeded.");
    }

    private static void CreateShortcut()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available on this system.");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");

        try
        {
            var shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, [ShortcutPath])
                ?? throw new InvalidOperationException("Could not create the Start Menu shortcut.");

            var shortcutType = shortcut.GetType();
            void Set(string name, object value) =>
                shortcutType.InvokeMember(name, BindingFlags.SetProperty, null, shortcut, [value]);

            Set("TargetPath", SettingsExe);
            Set("WorkingDirectory", SettingsDir);
            Set("IconLocation", SettingsExe + ",0");
            Set("Description", "Configure FolderFunBg folder backgrounds");
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }

        Log("Start Menu shortcut created: " + ShortcutPath);
    }

    private static void CopyFileWithRetry(string source, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20)
                {
                    throw new IOException(
                        $"Could not replace {destination}. Close any program using it and try again.",
                        exception);
                }

                // Windows may auto-restart Explorer after we stop it; kill it again
                // so the loaded hook DLL is released before the next attempt.
                StopProcesses("explorer", "FolderFunBg.Settings");
                Thread.Sleep(250);
            }
        }
    }

    private static void CopyDirectoryWithRetry(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            CopyFileWithRetry(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20)
                {
                    throw new IOException(
                        $"Could not remove {directory}. Close any program using it and try again.",
                        exception);
                }

                StopProcesses("explorer", "FolderFunBg.Settings");
                Thread.Sleep(250);
            }
        }
    }

    private static void StopProcesses(params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3_000);
                }
                catch
                {
                    // A process can exit between enumeration and Kill.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static void RestartExplorer()
    {
        // Explorer is often auto-restarted by Windows a moment after it is
        // killed. Stop it twice so the instance we start below is guaranteed
        // to read the current registration and load the new hook.
        StopExplorerAndWait();
        Thread.Sleep(300);
        StopExplorerAndWait();

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            Thread.Sleep(500);
            Log("Explorer restarted.");
        }
        catch (Exception exception)
        {
            Log("Could not start Explorer: " + exception.Message);
        }
    }

    private static void StopExplorerAndWait()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var processes = Process.GetProcessesByName("explorer");
            if (processes.Length == 0)
            {
                return;
            }

            foreach (var process in processes)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3_000);
                }
                catch
                {
                    // A process can exit between enumeration and Kill.
                }
                finally
                {
                    process.Dispose();
                }
            }

            Thread.Sleep(200);
        }
    }

    private static void StartExplorer()
    {
        if (Process.GetProcessesByName("explorer").Length > 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            Thread.Sleep(500);
            Log("Explorer started.");
        }
        catch (Exception exception)
        {
            Log("Could not start Explorer: " + exception.Message);
        }
    }

    private static void DeleteRegistryKey(string path)
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch (Exception exception)
        {
            Log($"Registry cleanup warning ({path}): {exception.Message}");
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Log("Temp cleanup warning: " + exception.Message);
        }
    }

    private static void Notify(string text, string caption, uint icon)
    {
        Log($"{caption}: {text.Replace('\n', ' ')}");
        if (!_silent)
        {
            MessageBoxW(IntPtr.Zero, text, caption, icon);
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Logging must never break setup.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
