#pragma once

#include "framework.h"

namespace FolderFunBg
{
    enum class Position
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
        Stretch,
        ZoomFill,
    };

    enum class ReadabilityMode
    {
        Auto,
        None,
        Light,
        Dark,
    };

    struct ReadabilitySettings
    {
        ReadabilityMode mode = ReadabilityMode::Auto;
        int strength = 96;
        bool gradient = true;
    };

    struct Rule
    {
        std::wstring path;
        std::wstring clsid;
        std::wstring image;
        std::optional<Position> position;
        std::optional<int> alpha;
        std::optional<bool> random;
        std::optional<ReadabilitySettings> readability;
        bool recursive = false;
        bool enabled = true;
    };

    struct Defaults
    {
        std::wstring image;
        Position position = Position::BottomRight;
        int alpha = 255;
        bool random = false;
    };

    struct ResolvedBackground
    {
        std::wstring image;
        Position position = Position::BottomRight;
        int alpha = 255;
        bool random = false;
        ReadabilitySettings readability;
        bool matchedRule = false;
    };

    class Config
    {
    public:
        Config() = default;

        static std::shared_ptr<const Config> LoadFromFile(const std::wstring& path);

        ResolvedBackground Resolve(const std::wstring& location) const;
        std::wstring ImagePath(const std::wstring& fileName) const;
        std::wstring PickRandomImage() const;

        const std::wstring& ImagesRoot() const { return imagesRoot_; }
        const std::vector<std::wstring>& ImageFiles() const { return imageFiles_; }
        const Defaults& DefaultSettings() const { return defaults_; }
        bool LightTheme() const { return lightTheme_; }

    private:
        std::wstring imagesRoot_;
        Defaults defaults_;
        ReadabilitySettings readability_;
        std::vector<Rule> folders_;
        std::vector<std::wstring> imageFiles_;
        bool lightTheme_ = true;
    };
}
