using FolderFunBg.Settings.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FolderFunBg.Settings;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 820));
        }
        catch (InvalidOperationException)
        {
        }

        NavView.SelectedItem = NavView.MenuItems[0];
    }

    public void NavigateForSmokeTest(Type pageType)
    {
        ContentFrame.Navigate(pageType);
        ContentFrame.UpdateLayout();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "folders" => typeof(FoldersPage),
            "library" => typeof(LibraryPage),
            "defaults" => typeof(DefaultsPage),
            "settings" => typeof(SettingsPage),
            _ => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
