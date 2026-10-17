#include "ImageCache.h"

#include "PathUtils.h"

namespace FolderFunBg
{
    CachedImage::CachedImage(const std::wstring& path)
    {
        std::ifstream stream(std::filesystem::path(path), std::ios::binary);
        if (!stream)
        {
            return;
        }

        std::vector<BYTE> bytes(
            (std::istreambuf_iterator<char>(stream)),
            std::istreambuf_iterator<char>());
        if (bytes.empty())
        {
            return;
        }

        stream_ = SHCreateMemStream(bytes.data(), static_cast<UINT>(bytes.size()));
        if (stream_ == nullptr)
        {
            return;
        }

        bitmap_ = Gdiplus::Bitmap::FromStream(stream_, FALSE);
        if (bitmap_ == nullptr || bitmap_->GetLastStatus() != Gdiplus::Ok)
        {
            return;
        }

        size_ = {
            static_cast<LONG>(bitmap_->GetWidth()),
            static_cast<LONG>(bitmap_->GetHeight()),
        };
        if (size_.cx <= 0 || size_.cy <= 0)
        {
            return;
        }

        if (bitmap_->GetHBITMAP(Gdiplus::Color(0, 0, 0, 0), &hBitmap_) != Gdiplus::Ok ||
            hBitmap_ == nullptr)
        {
            return;
        }

        memDc_ = CreateCompatibleDC(nullptr);
        if (memDc_ == nullptr)
        {
            return;
        }

        oldBitmap_ = SelectObject(memDc_, hBitmap_);
        luminance_ = ComputeLuminance();
    }

    CachedImage::~CachedImage()
    {
        if (memDc_ != nullptr)
        {
            if (oldBitmap_ != nullptr)
            {
                SelectObject(memDc_, oldBitmap_);
            }
            DeleteDC(memDc_);
        }

        if (hBitmap_ != nullptr)
        {
            DeleteObject(hBitmap_);
        }

        delete bitmap_;

        if (stream_ != nullptr)
        {
            stream_->Release();
        }
    }

    float CachedImage::ComputeLuminance()
    {
        if (bitmap_ == nullptr || size_.cx <= 0 || size_.cy <= 0)
        {
            return 0.5f;
        }

        constexpr int SamplesX = 8;
        constexpr int SamplesY = 8;

        double total = 0.0;
        double weights = 0.0;

        for (int y = 0; y < SamplesY; ++y)
        {
            for (int x = 0; x < SamplesX; ++x)
            {
                const int px = static_cast<int>((x + 0.5) * size_.cx / SamplesX);
                const int py = static_cast<int>((y + 0.5) * size_.cy / SamplesY);

                Gdiplus::Color color;
                if (bitmap_->GetPixel(px, py, &color) != Gdiplus::Ok)
                {
                    continue;
                }

                const double weight = color.GetA() / 255.0;
                if (weight < 0.05)
                {
                    continue;
                }

                const double luminance =
                    0.2126 * (color.GetR() / 255.0) +
                    0.7152 * (color.GetG() / 255.0) +
                    0.0722 * (color.GetB() / 255.0);

                total += luminance * weight;
                weights += weight;
            }
        }

        return weights <= 0.0 ? 0.5f : static_cast<float>(total / weights);
    }

    void CachedImage::Draw(HDC hdc, int x, int y, int width, int height, BYTE alpha)
    {
        if (!IsValid() || hdc == nullptr || width <= 0 || height <= 0)
        {
            return;
        }

        std::lock_guard lock(drawMutex_);

        BLENDFUNCTION blend{};
        blend.BlendOp = AC_SRC_OVER;
        blend.BlendFlags = 0;
        blend.SourceConstantAlpha = alpha;
        blend.AlphaFormat = AC_SRC_ALPHA;

        AlphaBlend(
            hdc,
            x,
            y,
            width,
            height,
            memDc_,
            0,
            0,
            size_.cx,
            size_.cy,
            blend);
    }

    void DrawReadabilityScrim(
        HDC hdc,
        const RECT& rect,
        const ReadabilitySettings& settings,
        bool lightTheme,
        float imageLuminance)
    {
        if (hdc == nullptr || rect.right <= rect.left || rect.bottom <= rect.top)
        {
            return;
        }

        int alpha = std::clamp(settings.strength, 0, 255);
        bool useLight = false;

        switch (settings.mode)
        {
        case ReadabilityMode::None:
            return;
        case ReadabilityMode::Light:
            useLight = true;
            break;
        case ReadabilityMode::Dark:
            useLight = false;
            break;
        case ReadabilityMode::Auto:
            if (lightTheme)
            {
                if (imageLuminance >= 0.45f)
                {
                    return;
                }

                useLight = true;
                alpha = static_cast<int>(alpha * (1.0f - imageLuminance / 0.45f));
            }
            else
            {
                if (imageLuminance <= 0.55f)
                {
                    return;
                }

                useLight = false;
                alpha = static_cast<int>(alpha * ((imageLuminance - 0.55f) / 0.45f));
            }
            break;
        }

        alpha = std::clamp(alpha, 0, 255);
        if (alpha == 0)
        {
            return;
        }

        const BYTE color = useLight ? 255 : 0;
        const int width = rect.right - rect.left;
        const int height = rect.bottom - rect.top;

        Gdiplus::Graphics graphics(hdc);
        graphics.SetCompositingMode(Gdiplus::CompositingModeSourceOver);

        if (settings.gradient)
        {
            Gdiplus::LinearGradientBrush brush(
                Gdiplus::Point(rect.left, rect.top),
                Gdiplus::Point(rect.right, rect.top),
                Gdiplus::Color(static_cast<BYTE>(alpha), color, color, color),
                Gdiplus::Color(0, color, color, color));
            graphics.FillRectangle(&brush, rect.left, rect.top, width, height);
        }
        else
        {
            Gdiplus::SolidBrush brush(
                Gdiplus::Color(static_cast<BYTE>(alpha), color, color, color));
            graphics.FillRectangle(&brush, rect.left, rect.top, width, height);
        }

        graphics.Flush();
    }

    std::shared_ptr<CachedImage> ImageCache::Get(const std::wstring& path)
    {
        if (path.empty())
        {
            return nullptr;
        }

        std::lock_guard lock(mutex_);

        if (const auto found = cache_.find(path); found != cache_.end())
        {
            return found->second;
        }

        if (failed_.contains(path))
        {
            return nullptr;
        }

        auto image = std::make_shared<CachedImage>(path);
        if (!image->IsValid())
        {
            failed_.insert(path);
            return nullptr;
        }

        cache_.emplace(path, image);
        return image;
    }

    void ImageCache::Clear()
    {
        std::lock_guard lock(mutex_);
        cache_.clear();
        failed_.clear();
    }
}
