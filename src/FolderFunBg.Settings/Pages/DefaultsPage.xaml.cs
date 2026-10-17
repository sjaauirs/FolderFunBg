using FolderFunBg.Core;
using FolderFunBg.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.UI;

namespace FolderFunBg.Settings.Pages;

public sealed partial class DefaultsPage : Page
{
    private bool _loading;

    public DefaultsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadValues();
    }

    private void LoadValues()
    {
        _loading = true;

        var images = AppServices.ImageFileNames();
        ImageBox.ItemsSource = images;
        ImageBox.SelectedItem = AppServices.Config.Defaults.Image;

        PositionBox.ItemsSource = BackgroundPositions.All;
        PositionBox.SelectedItem = AppServices.Config.Defaults.Position;
        AlphaBox.Value = AppServices.Config.Defaults.Alpha;
        RandomSwitch.IsOn = AppServices.Config.Defaults.Random;

        ReadabilityBox.ItemsSource = ReadabilitySettings.AllModes;
        ReadabilityBox.SelectedItem = AppServices.Config.Readability.Mode;
        ScrimBox.Value = AppServices.Config.Readability.Strength;
        GradientSwitch.IsOn = AppServices.Config.Readability.Gradient;

        _loading = false;
        UpdatePreview();
    }

    private void Preview_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void Preview_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdatePreview();

    private void Preview_Toggled(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (_loading)
        {
            return;
        }

        var selected = ImageBox.SelectedItem as string;
        var path = AppServices.ResolveImagePath(selected);
        PreviewImage.Source = path is not null && File.Exists(path)
            ? new BitmapImage(new Uri(path)) { DecodePixelWidth = 480 }
            : null;

        var mode = ReadabilityBox.SelectedItem as string ?? ReadabilitySettings.Auto;
        var strength = (int)Math.Clamp(ScrimBox.Value, 0, 255);
        var gradient = GradientSwitch.IsOn;

        if (string.Equals(mode, ReadabilitySettings.None, StringComparison.OrdinalIgnoreCase) || strength == 0)
        {
            ScrimRect.Fill = new SolidColorBrush(Colors.Transparent);
            return;
        }

        bool useLight;
        if (string.Equals(mode, ReadabilitySettings.Light, StringComparison.OrdinalIgnoreCase))
        {
            useLight = true;
        }
        else if (string.Equals(mode, ReadabilitySettings.Dark, StringComparison.OrdinalIgnoreCase))
        {
            useLight = false;
        }
        else
        {
            useLight = Application.Current.RequestedTheme != ApplicationTheme.Dark;
        }

        var color = useLight ? Colors.White : Colors.Black;

        if (gradient)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
            };
            brush.GradientStops.Add(new GradientStop
            {
                Color = Color.FromArgb((byte)strength, color.R, color.G, color.B),
                Offset = 0,
            });
            brush.GradientStops.Add(new GradientStop
            {
                Color = Colors.Transparent,
                Offset = 1,
            });
            ScrimRect.Fill = brush;
        }
        else
        {
            ScrimRect.Fill = new SolidColorBrush(
                Color.FromArgb((byte)strength, color.R, color.G, color.B));
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppServices.Config.Defaults.Image = ImageBox.SelectedItem as string;
            AppServices.Config.Defaults.Position = PositionBox.SelectedItem as string ?? BackgroundPositions.BottomRight;
            AppServices.Config.Defaults.Alpha = (int)Math.Round(AlphaBox.Value);
            AppServices.Config.Defaults.Random = RandomSwitch.IsOn;

            AppServices.Config.Readability.Mode =
                ReadabilityBox.SelectedItem as string ?? ReadabilitySettings.Auto;
            AppServices.Config.Readability.Strength = (int)Math.Round(ScrimBox.Value);
            AppServices.Config.Readability.Gradient = GradientSwitch.IsOn;

            AppServices.Save();
            ShowStatus("Defaults saved.", InfoBarSeverity.Success);
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
