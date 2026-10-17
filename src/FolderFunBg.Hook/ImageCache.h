#pragma once

#include "framework.h"
#include "Config.h"

namespace FolderFunBg
{
    class CachedImage
    {
    public:
        explicit CachedImage(const std::wstring& path);
        ~CachedImage();

        CachedImage(const CachedImage&) = delete;
        CachedImage& operator=(const CachedImage&) = delete;

        bool IsValid() const
        {
            return hBitmap_ != nullptr && memDc_ != nullptr && size_.cx > 0 && size_.cy > 0;
        }

        SIZE Size() const { return size_; }
        float Luminance() const { return luminance_; }

        void Draw(HDC hdc, int x, int y, int width, int height, BYTE alpha);

    private:
        float ComputeLuminance();

        Gdiplus::Bitmap* bitmap_ = nullptr;
        IStream* stream_ = nullptr;
        HBITMAP hBitmap_ = nullptr;
        HGDIOBJ oldBitmap_ = nullptr;
        HDC memDc_ = nullptr;
        SIZE size_{};
        float luminance_ = 0.5f;
        std::mutex drawMutex_;
    };

    void DrawReadabilityScrim(
        HDC hdc,
        const RECT& rect,
        const ReadabilitySettings& settings,
        bool lightTheme,
        float imageLuminance);

    class ImageCache
    {
    public:
        std::shared_ptr<CachedImage> Get(const std::wstring& path);
        void Clear();

    private:
        std::mutex mutex_;
        std::unordered_map<std::wstring, std::shared_ptr<CachedImage>> cache_;
        std::unordered_set<std::wstring> failed_;
    };
}
