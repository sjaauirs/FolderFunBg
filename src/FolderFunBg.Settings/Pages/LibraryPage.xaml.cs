using System.Collections.ObjectModel;
using System.Diagnostics;
using FolderFunBg.Settings.Models;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FolderFunBg.Settings.Pages;

public sealed partial class LibraryPage : Page
{
    public LibraryPage()
    {
        InitializeComponent();
    }

    public ObservableCollection<ImageItem> Images { get; } = [];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void Refresh()
    {
        Images.Clear();
        foreach (var info in AppServices.Images.List())
        {
            Images.Add(ImageItem.FromInfo(info));
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var window = App.CurrentWindow;
        if (window is null)
        {
            return;
        }

        var paths = await PickerHelper.PickImagesAsync(window);
        var imported = 0;
        var errors = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                AppServices.Images.Import(path);
                imported++;
            }
            catch (Exception exception)
            {
                errors.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        Refresh();
        var message = $"{imported} image(s) imported.";
        if (errors.Count > 0)
        {
            message += " " + string.Join(" ", errors);
        }

        ShowStatus(message, errors.Count > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ImageItem item })
        {
            return;
        }

        var usedBy = AppServices.Config.Folders.Count(rule =>
            string.Equals(rule.Image, item.FileName, StringComparison.OrdinalIgnoreCase));

        if (usedBy > 0)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete image?",
                Content = $"This image is used by {usedBy} folder rule(s). Delete it anyway?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            AppServices.Images.Delete(item.FileName);
            Refresh();
            ShowStatus($"Deleted {item.FileName}.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppServices.ImagesRoot,
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
