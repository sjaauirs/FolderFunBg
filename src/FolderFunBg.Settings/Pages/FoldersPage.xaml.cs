using System.Collections.ObjectModel;
using FolderFunBg.Settings.Models;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FolderFunBg.Settings.Pages;

public sealed partial class FoldersPage : Page
{
    public FoldersPage()
    {
        InitializeComponent();
    }

    public ObservableCollection<FolderRuleItem> Rules { get; } = [];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadRules();
    }

    private void LoadRules()
    {
        Rules.Clear();
        var imageNames = AppServices.ImageFileNames().ToList();

        foreach (var rule in AppServices.Config.Folders)
        {
            var item = FolderRuleItem.FromRule(rule);
            if (rule.Alpha is null)
            {
                item.Alpha = AppServices.Config.Defaults.Alpha;
            }

            if (rule.Readability?.Strength is null)
            {
                item.ReadabilityStrength = AppServices.Config.Readability.Strength;
            }

            if (!string.IsNullOrWhiteSpace(item.Image) &&
                !imageNames.Contains(item.Image, StringComparer.OrdinalIgnoreCase))
            {
                imageNames.Add(item.Image);
            }

            item.ImageOptions = imageNames.ToArray();
            Rules.Add(item);
        }

        if (AppServices.StartupError is not null)
        {
            ShowStatus($"Configuration error: {AppServices.StartupError}", InfoBarSeverity.Warning);
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var window = App.CurrentWindow;
        if (window is null)
        {
            return;
        }

        var folder = await PickerHelper.PickFolderAsync(window);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var imagePath = await PickerHelper.PickImageAsync(window);
        string? imageName = null;
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            try
            {
                imageName = AppServices.Images.Import(imagePath).FileName;
            }
            catch (Exception exception)
            {
                ShowStatus(exception.Message, InfoBarSeverity.Error);
                return;
            }
        }

        var item = new FolderRuleItem
        {
            Path = folder,
            Image = imageName,
            Position = AppServices.Config.Defaults.Position,
            Alpha = AppServices.Config.Defaults.Alpha,
            ReadabilityStrength = AppServices.Config.Readability.Strength,
            ImageOptions = AppServices.ImageFileNames(),
        };

        Rules.Add(item);
        ApplyRules();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FolderRuleItem item })
        {
            Rules.Remove(item);
            ApplyRules();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplyRules();

    private void ApplyRules()
    {
        try
        {
            AppServices.Config.Folders = Rules.Select(rule => rule.ToRule()).ToList();
            AppServices.Save();
            ShowStatus("Saved. Open Explorer windows refresh automatically.", InfoBarSeverity.Success);
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
