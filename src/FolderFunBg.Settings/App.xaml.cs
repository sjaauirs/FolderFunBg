using FolderFunBg.Settings.Pages;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml;

namespace FolderFunBg.Settings;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    public static Window? CurrentWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppServices.Initialize();
        var window = new MainWindow();
        CurrentWindow = window;
        window.Activate();

        if (Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
        {
            _ = RunSmokeTestAsync(window);
        }
    }

    private static async Task RunSmokeTestAsync(MainWindow window)
    {
        var results = new List<string>();
        var failed = false;

        try
        {
            await Task.Delay(400);

            Type[] pages =
            [
                typeof(FoldersPage),
                typeof(LibraryPage),
                typeof(DefaultsPage),
                typeof(SettingsPage),
            ];

            foreach (var pageType in pages)
            {
                try
                {
                    window.NavigateForSmokeTest(pageType);
                    await Task.Delay(400);
                    results.Add($"PASS {pageType.Name}");
                }
                catch (Exception exception)
                {
                    failed = true;
                    results.Add($"FAIL {pageType.Name}: {exception}");
                }
            }
        }
        catch (Exception exception)
        {
            failed = true;
            results.Add($"FAIL startup: {exception}");
        }

        if (AppServices.StartupError is not null)
        {
            failed = true;
        }

        results.Add($"StartupError: {AppServices.StartupError ?? "(none)"}");

        var path = Path.Combine(Path.GetTempPath(), "FolderFunBg-smoke.txt");
        await File.WriteAllLinesAsync(path, results);
        window.Close();
        Environment.Exit(failed ? 1 : 0);
    }
}
