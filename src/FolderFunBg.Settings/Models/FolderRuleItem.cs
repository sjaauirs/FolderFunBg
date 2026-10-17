using System.ComponentModel;
using System.Runtime.CompilerServices;
using FolderFunBg.Core;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FolderFunBg.Settings.Models;

public sealed class FolderRuleItem : INotifyPropertyChanged
{
    private string? _path;
    private string? _clsid;
    private string? _image;
    private string _position = BackgroundPositions.BottomRight;
    private double _alpha = 255;
    private bool _recursive;
    private bool _enabled = true;
    private string _readabilityMode = "inherit";
    private double _readabilityStrength = 96;

    public IReadOnlyList<string> PositionOptions => BackgroundPositions.All;

    public IReadOnlyList<string> ReadabilityModeOptions { get; } =
        ["inherit", "auto", "light", "dark", "none"];

    public string? Path
    {
        get => _path;
        set
        {
            if (_path == value)
            {
                return;
            }

            _path = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPath));
        }
    }

    public string? Clsid
    {
        get => _clsid;
        set
        {
            if (_clsid == value)
            {
                return;
            }

            _clsid = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPath));
        }
    }

    public string? Image
    {
        get => _image;
        set
        {
            if (_image == value)
            {
                return;
            }

            _image = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Thumbnail));
        }
    }

    public string Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            OnPropertyChanged();
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            if (Math.Abs(_alpha - value) < 0.001)
            {
                return;
            }

            _alpha = value;
            OnPropertyChanged();
        }
    }

    public bool Recursive
    {
        get => _recursive;
        set
        {
            if (_recursive == value)
            {
                return;
            }

            _recursive = value;
            OnPropertyChanged();
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnPropertyChanged();
        }
    }

    public string ReadabilityMode
    {
        get => _readabilityMode;
        set
        {
            if (_readabilityMode == value)
            {
                return;
            }

            _readabilityMode = value;
            OnPropertyChanged();
        }
    }

    public double ReadabilityStrength
    {
        get => _readabilityStrength;
        set
        {
            if (Math.Abs(_readabilityStrength - value) < 0.001)
            {
                return;
            }

            _readabilityStrength = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> ImageOptions { get; set; } = [];

    public string DisplayPath => Path ?? Clsid ?? "(unknown folder)";

    public BitmapImage? Thumbnail => AppServices.CreateThumbnail(Image);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static FolderRuleItem FromRule(FolderRule rule) => new()
    {
        Path = rule.Path,
        Clsid = rule.Clsid,
        Image = rule.Image,
        Position = rule.Position ?? BackgroundPositions.BottomRight,
        Alpha = rule.Alpha ?? 255,
        Recursive = rule.Recursive,
        Enabled = rule.Enabled,
        ReadabilityMode = rule.Readability?.Mode ?? "inherit",
        ReadabilityStrength = rule.Readability?.Strength ?? 96,
    };

    public FolderRule ToRule()
    {
        var rule = new FolderRule
        {
            Path = string.IsNullOrWhiteSpace(Path) ? null : Path,
            Clsid = string.IsNullOrWhiteSpace(Path) ? Clsid : null,
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            Position = string.IsNullOrWhiteSpace(Position) ? null : Position,
            Alpha = (int)Math.Round(Alpha),
            Recursive = Recursive,
            Enabled = Enabled,
        };

        if (!string.Equals(ReadabilityMode, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            rule.Readability = new ReadabilityOverride
            {
                Mode = ReadabilityMode,
                Strength = (int)Math.Round(ReadabilityStrength),
            };
        }

        return rule;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
