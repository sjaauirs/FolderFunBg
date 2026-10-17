using System.Diagnostics;
using Microsoft.Win32;

namespace FolderFunBg.Settings.Services;

public sealed record ExtensionStatus(
    bool Registered,
    string? DllPath,
    bool LegacyToolPresent,
    bool LoadedInExplorer);

public static class ExtensionStatusService
{
    public const string Clsid = "{80BDC533-D192-445A-A1D9-F91310021919}";
    private const string LegacyClsid = "{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}";

    public static ExtensionStatus GetStatus()
    {
        string? dllPath = null;
        using (var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InProcServer32"))
        {
            dllPath = key?.GetValue(null) as string;
        }

        var registered = !string.IsNullOrWhiteSpace(dllPath) && File.Exists(dllPath);

        using var legacyKey = Registry.LocalMachine.OpenSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{LegacyClsid}");
        var legacyToolPresent = legacyKey is not null;

        var loaded = false;
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                if (process.Modules.Cast<ProcessModule>().Any(module =>
                        string.Equals(module.ModuleName, "FolderFunBg.Hook.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    loaded = true;
                    break;
                }
            }
            catch (InvalidOperationException)
            {
                // Explorer exited or the module list is unavailable.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied for a protected process.
            }
            finally
            {
                process.Dispose();
            }
        }

        return new ExtensionStatus(registered, dllPath, legacyToolPresent, loaded);
    }
}
