using System.Diagnostics;
using FolderFunBg.Core;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FolderFunBg.Settings.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshStatus();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        var status = ExtensionStatusService.GetStatus();
        var lines = new List<string>
        {
            status.Registered
                ? $"Extension registered: {status.DllPath}"
                : "Extension is not registered.",
            status.LoadedInExplorer
                ? "Loaded in the running Explorer process."
                : "Not loaded in the running Explorer process. Restart Explorer after installing.",
            status.LegacyToolPresent
                ? "ExplorerBgTool is also registered. Unregister it before installing FolderFunBg."
                : "No conflicting ExplorerBgTool registration detected.",
        };

        if (AppServices.StartupError is not null)
        {
            lines.Add($"Config error: {AppServices.StartupError}");
        }

        StatusText.Text = string.Join(Environment.NewLine, lines);
        StatusBar.Severity = status.LegacyToolPresent ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
        StatusBar.Message = status.LegacyToolPresent
            ? "Resolve the ExplorerBgTool conflict before installing."
            : "Status refreshed.";
        StatusBar.IsOpen = true;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, "FolderFunBg.Hook.dll");
        if (!File.Exists(dllPath))
        {
            ShowStatus($"Native hook not found at {dllPath}. Build the hook first.", InfoBarSeverity.Error);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Install FolderFunBg",
            Content = "This registers the Explorer extension and restarts Explorer. Save your open work first.",
            PrimaryButtonText = "Install and restart Explorer",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        RunScript("install.ps1", $"-DllPath \"{dllPath}\" -RestartExplorer");
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Uninstall FolderFunBg",
            Content = "This removes the Explorer extension and restarts Explorer.",
            PrimaryButtonText = "Uninstall and restart Explorer",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        RunScript("uninstall.ps1", "-RestartExplorer");
    }

    private void RunScript(string scriptName, string arguments)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "installer", scriptName);
        if (!File.Exists(script))
        {
            ShowStatus($"Installer script not found at {script}.", InfoBarSeverity.Error);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" {arguments}")
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            Process.Start(startInfo);
            ShowStatus("Elevation prompt opened. Approve it to continue.", InfoBarSeverity.Informational);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            ShowStatus("Elevation was cancelled.", InfoBarSeverity.Warning);
        }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e) => OpenFolder(AppServices.Store.RootDirectory);

    private void OpenImages_Click(object sender, RoutedEventArgs e) => OpenFolder(AppServices.ImagesRoot);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(FolderFunBgPaths.DefaultLogsPath);

    private async void ImportLegacy_Click(object sender, RoutedEventArgs e)
    {
        var window = App.CurrentWindow;
        if (window is null)
        {
            return;
        }

        var iniPath = await PickerHelper.PickIniAsync(window);
        if (string.IsNullOrWhiteSpace(iniPath))
        {
            return;
        }

        try
        {
            var legacyDirectory = Path.Combine(
                Path.GetDirectoryName(iniPath) ?? AppServices.ImagesRoot,
                "Image");

            var imported = LegacyConfigImporter.Import(iniPath, AppServices.ImagesRoot, legacyDirectory);

            var sourceDirectory = Directory.Exists(imported.Config.ImagesRoot)
                ? imported.Config.ImagesRoot
                : legacyDirectory;

            var importedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(sourceDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(sourceDirectory)
                             .Where(ImageLibrary.HasSupportedExtension))
                {
                    try
                    {
                        var image = AppServices.Images.Import(file);
                        importedNames[Path.GetFileName(file)] = image.FileName;
                    }
                    catch (InvalidDataException)
                    {
                    }
                }
            }

            foreach (var rule in imported.Config.Folders)
            {
                if (!string.IsNullOrWhiteSpace(rule.Image) &&
                    importedNames.TryGetValue(rule.Image, out var newName))
                {
                    rule.Image = newName;
                }
            }

            if (!string.IsNullOrWhiteSpace(imported.Config.Defaults.Image) &&
                importedNames.TryGetValue(imported.Config.Defaults.Image, out var defaultName))
            {
                imported.Config.Defaults.Image = defaultName;
            }

            imported.Config.ImagesRoot = AppServices.ImagesRoot;
            AppServices.Config.Defaults = imported.Config.Defaults;
            AppServices.Config.Readability = imported.Config.Readability;
            AppServices.Config.Folders = imported.Config.Folders;
            AppServices.Save();

            var warnings = imported.Warnings.Count > 0
                ? " " + string.Join(" ", imported.Warnings)
                : string.Empty;
            ShowStatus(
                $"Imported {imported.Config.Folders.Count} folder rule(s).{warnings}",
                imported.Warnings.Count > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Severity = severity;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
