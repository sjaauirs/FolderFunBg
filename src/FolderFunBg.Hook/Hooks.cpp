#include "Hooks.h"

#include "AppState.h"
#include "ImageCache.h"
#include "Logging.h"
#include "PathUtils.h"

#include "../../third_party/minhook/MinHook.h"

#include <oleacc.h>

namespace FolderFunBg::Hooks
{
    namespace
    {
        struct WindowState
        {
            HDC hdc = nullptr;
        };

        std::mutex g_stateMutex;
        std::unordered_map<DWORD, std::unordered_map<HWND, WindowState>> g_windows;
        std::unordered_map<DWORD, std::wstring> g_locations;
        std::atomic<bool> g_installed{false};
        std::atomic<bool> g_duiAvailable{false};

        using PaintBackground_t = void(__fastcall*)(void*, HDC, void*, const RECT*, const RECT*, const RECT*, const RECT*);
        using ElementGetAccRole_t = int(__fastcall*)(void*);
        using ElementGetRoot_t = void*(__fastcall*)(void*);
        using ElementGetRootRelativeBounds_t = long(__fastcall*)(void*, RECT*);

        PaintBackground_t g_originalPaintBackground = nullptr;
        ElementGetAccRole_t g_getAccRole = nullptr;
        ElementGetRoot_t g_getRoot = nullptr;
        ElementGetRootRelativeBounds_t g_getRootRelativeBounds = nullptr;

        std::wstring RectText(const RECT* rect)
        {
            if (rect == nullptr)
            {
                return L"null";
            }

            std::wostringstream stream;
            stream << L"(" << rect->left << L","
                   << rect->top << L","
                   << rect->right << L","
                   << rect->bottom << L")";
            return stream.str();
        }

        thread_local HDC g_duiHdc = nullptr;
        thread_local bool g_duiDrewForHdc = false;

        void DrawConfiguredBackground(HDC hdc, const RECT& area, int triggerRole)
        {
            try
            {
                auto config = AppState::Instance().GetConfig();
                if (!config || hdc == nullptr)
                {
                    return;
                }

                std::wstring location;
                {
                    std::lock_guard lock(g_stateMutex);
                    const auto iterator = g_locations.find(GetCurrentThreadId());
                    if (iterator != g_locations.end())
                    {
                        location = iterator->second;
                    }
                }

                const auto resolved = config->Resolve(location);
                if (resolved.alpha <= 0)
                {
                    return;
                }

                const std::wstring fileName = resolved.random
                    ? config->PickRandomImage()
                    : resolved.image;
                const auto imagePath = config->ImagePath(fileName);
                if (imagePath.empty())
                {
                    return;
                }

                auto image = AppState::Instance().GetImageCache().Get(imagePath);
                if (!image)
                {
                    return;
                }

                const int areaWidth = area.right - area.left;
                const int areaHeight = area.bottom - area.top;
                if (areaWidth <= 0 || areaHeight <= 0)
                {
                    return;
                }

                const SIZE imageSize = image->Size();
                int x = 0;
                int y = 0;
                int width = imageSize.cx;
                int height = imageSize.cy;

                switch (resolved.position)
                {
                case Position::TopLeft:
                    break;
                case Position::TopRight:
                    x = areaWidth - imageSize.cx;
                    break;
                case Position::BottomLeft:
                    y = areaHeight - imageSize.cy;
                    break;
                case Position::BottomRight:
                    x = areaWidth - imageSize.cx;
                    y = areaHeight - imageSize.cy;
                    break;
                case Position::Center:
                    x = (areaWidth - imageSize.cx) / 2;
                    y = (areaHeight - imageSize.cy) / 2;
                    break;
                case Position::Stretch:
                    x = 0;
                    y = 0;
                    width = areaWidth;
                    height = areaHeight;
                    break;
                case Position::ZoomFill:
                {
                    const double scale = std::max(
                        static_cast<double>(areaWidth) / imageSize.cx,
                        static_cast<double>(areaHeight) / imageSize.cy);
                    width = static_cast<int>(std::lround(imageSize.cx * scale));
                    height = static_cast<int>(std::lround(imageSize.cy * scale));
                    x = (areaWidth - width) / 2;
                    y = (areaHeight - height) / 2;
                    break;
                }
                }

                const int saved = SaveDC(hdc);
                if (saved == 0)
                {
                    return;
                }

                SelectClipRgn(hdc, nullptr);
                IntersectClipRect(hdc, area.left, area.top, area.right, area.bottom);
                image->Draw(
                    hdc,
                    area.left + x,
                    area.top + y,
                    width,
                    height,
                    static_cast<BYTE>(resolved.alpha));

                RECT imageRect{
                    area.left + x,
                    area.top + y,
                    area.left + x + width,
                    area.top + y + height};
                DrawReadabilityScrim(
                    hdc,
                    imageRect,
                    resolved.readability,
                    config->LightTheme(),
                    image->Luminance());
                RestoreDC(hdc, saved);

                static std::atomic<int> drawLogCount{0};
                if (drawLogCount.fetch_add(1) < 20)
                {
                    Logging::Info(
                        L"DUI-DRAW role=" + std::to_wstring(triggerRole) +
                        L" area=" + RectText(&area) +
                        L" image=" + imagePath);
                }
            }
            catch (...)
            {
                Logging::Error(L"DUI draw failed.");
            }
        }

        void __fastcall HookPaintBackground(
            void* self,
            HDC hdc,
            void* value,
            const RECT* rect,
            const RECT* clip,
            const RECT* exclude,
            const RECT* target)
        {
            if (g_originalPaintBackground != nullptr)
            {
                g_originalPaintBackground(self, hdc, value, rect, clip, exclude, target);
            }

            try
            {
                if (self == nullptr || hdc == nullptr || rect == nullptr)
                {
                    return;
                }

                const int role = g_getAccRole != nullptr ? g_getAccRole(self) : -1;
                if (role != ROLE_SYSTEM_LIST && role != ROLE_SYSTEM_LISTITEM)
                {
                    return;
                }

                if (hdc != g_duiHdc)
                {
                    g_duiHdc = hdc;
                    g_duiDrewForHdc = false;
                }

                if (g_duiDrewForHdc)
                {
                    return;
                }

                void* root = g_getRoot != nullptr ? g_getRoot(self) : nullptr;
                RECT listRect{};
                if (root != nullptr && g_getRootRelativeBounds != nullptr)
                {
                    g_getRootRelativeBounds(root, &listRect);
                }

                if (listRect.right - listRect.left > 300 &&
                    listRect.bottom - listRect.top > 200)
                {
                    g_duiDrewForHdc = true;
                    DrawConfiguredBackground(hdc, listRect, role);
                }
            }
            catch (...)
            {
                Logging::Error(L"DUI background paint failed.");
            }
        }

        bool InstallDuiBackground()
        {
            HMODULE module = GetModuleHandleW(L"dui70.dll");
            if (module == nullptr)
            {
                module = LoadLibraryExW(L"dui70.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
            }

            if (module == nullptr)
            {
                Logging::Error(L"DUI background:dui70.dll is not available.");
                return false;
            }

            const auto paint = GetProcAddress(
                module,
                "?PaintBackground@Element@DirectUI@@QEAAXPEAUHDC__@@PEAVValue@2@AEBUtagRECT@@222@Z");
            if (paint == nullptr)
            {
                Logging::Error(L"DUI background:PaintBackground export not found.");
                return false;
            }

            g_getAccRole = reinterpret_cast<ElementGetAccRole_t>(
                GetProcAddress(module, "?GetAccRole@Element@DirectUI@@QEAAHXZ"));
            g_getRoot = reinterpret_cast<ElementGetRoot_t>(
                GetProcAddress(module, "?GetRoot@Element@DirectUI@@QEAAPEAV12@XZ"));
            g_getRootRelativeBounds = reinterpret_cast<ElementGetRootRelativeBounds_t>(
                GetProcAddress(module, "?GetRootRelativeBounds@Element@DirectUI@@QEAAJPEAUtagRECT@@@Z"));

            if (g_getAccRole == nullptr ||
                g_getRoot == nullptr ||
                g_getRootRelativeBounds == nullptr)
            {
                Logging::Error(L"DUI background:required element exports not found.");
                return false;
            }

            if (MH_CreateHook(
                    paint,
                    reinterpret_cast<LPVOID>(&HookPaintBackground),
                    reinterpret_cast<LPVOID*>(&g_originalPaintBackground)) != MH_OK)
            {
                Logging::Error(L"DUI background:MinHook could not hook PaintBackground.");
                return false;
            }

            Logging::Info(L"DUI background: PaintBackground hook installed.");
            return true;
        }

        using CreateWindowExW_t = HWND(WINAPI*)(DWORD, LPCWSTR, LPCWSTR, DWORD, int, int, int, int, HWND, HMENU, HINSTANCE, LPVOID);
        using DestroyWindow_t = BOOL(WINAPI*)(HWND);
        using BeginPaint_t = HDC(WINAPI*)(HWND, LPPAINTSTRUCT);
        using FillRect_t = int(WINAPI*)(HDC, const RECT*, HBRUSH);
        using CreateCompatibleDC_t = HDC(WINAPI*)(HDC);

        CreateWindowExW_t g_originalCreateWindowExW = nullptr;
        DestroyWindow_t g_originalDestroyWindow = nullptr;
        BeginPaint_t g_originalBeginPaint = nullptr;
        FillRect_t g_originalFillRect = nullptr;
        CreateCompatibleDC_t g_originalCreateCompatibleDC = nullptr;

        void TrackWindow(HWND hwnd)
        {
            if (hwnd == nullptr)
            {
                return;
            }

            std::lock_guard lock(g_stateMutex);
            g_windows[GetCurrentThreadId()][hwnd] = WindowState{};
        }

        void UntrackWindow(HWND hwnd)
        {
            std::lock_guard lock(g_stateMutex);
            const auto thread = g_windows.find(GetCurrentThreadId());
            if (thread == g_windows.end())
            {
                return;
            }

            thread->second.erase(hwnd);
            if (thread->second.empty())
            {
                g_windows.erase(thread);
                g_locations.erase(GetCurrentThreadId());
            }
        }

        bool FindWindowLocked(
            DWORD threadId,
            HDC hdc,
            HWND& hwnd,
            std::wstring& location)
        {
            const auto thread = g_windows.find(threadId);
            if (thread == g_windows.end())
            {
                return false;
            }

            for (const auto& [candidate, state] : thread->second)
            {
                if (state.hdc == hdc)
                {
                    hwnd = candidate;

                    const auto locationIterator = g_locations.find(threadId);
                    if (locationIterator != g_locations.end())
                    {
                        location = locationIterator->second;
                    }

                    return true;
                }
            }

            return false;
        }

        void DrawBackground(HDC hdc, const RECT* rect)
        {
            try
            {
                auto config = AppState::Instance().GetConfig();
                if (!config || hdc == nullptr || rect == nullptr)
                {
                    return;
                }

                const DWORD threadId = GetCurrentThreadId();
                HWND hwnd = nullptr;
                std::wstring location;

                {
                    std::lock_guard lock(g_stateMutex);
                    if (!FindWindowLocked(threadId, hdc, hwnd, location))
                    {
                        return;
                    }
                }

                const auto resolved = config->Resolve(location);
                if (resolved.alpha <= 0)
                {
                    return;
                }

                const std::wstring fileName = resolved.random
                    ? config->PickRandomImage()
                    : resolved.image;
                const auto imagePath = config->ImagePath(fileName);
                if (imagePath.empty())
                {
                    return;
                }

                auto image = AppState::Instance().GetImageCache().Get(imagePath);
                if (!image)
                {
                    return;
                }

                RECT client{};
                if (!GetClientRect(hwnd, &client))
                {
                    return;
                }

                const int clientWidth = client.right - client.left;
                const int clientHeight = client.bottom - client.top;
                if (clientWidth <= 0 || clientHeight <= 0)
                {
                    return;
                }

                const SIZE imageSize = image->Size();
                int x = 0;
                int y = 0;
                int width = imageSize.cx;
                int height = imageSize.cy;

                switch (resolved.position)
                {
                case Position::TopLeft:
                    break;
                case Position::TopRight:
                    x = clientWidth - imageSize.cx;
                    break;
                case Position::BottomLeft:
                    y = clientHeight - imageSize.cy;
                    break;
                case Position::BottomRight:
                    x = clientWidth - imageSize.cx;
                    y = clientHeight - imageSize.cy;
                    break;
                case Position::Center:
                    x = (clientWidth - imageSize.cx) / 2;
                    y = (clientHeight - imageSize.cy) / 2;
                    break;
                case Position::Stretch:
                    x = 0;
                    y = 0;
                    width = clientWidth;
                    height = clientHeight;
                    break;
                case Position::ZoomFill:
                {
                    const double scale = std::max(
                        static_cast<double>(clientWidth) / imageSize.cx,
                        static_cast<double>(clientHeight) / imageSize.cy);
                    width = static_cast<int>(std::lround(imageSize.cx * scale));
                    height = static_cast<int>(std::lround(imageSize.cy * scale));
                    x = (clientWidth - width) / 2;
                    y = (clientHeight - height) / 2;
                    break;
                }
                }

                const int saved = SaveDC(hdc);
                if (saved == 0)
                {
                    return;
                }

                IntersectClipRect(hdc, rect->left, rect->top, rect->right, rect->bottom);
                image->Draw(hdc, x, y, width, height, static_cast<BYTE>(resolved.alpha));
                DrawReadabilityScrim(
                    hdc,
                    client,
                    resolved.readability,
                    config->LightTheme(),
                    image->Luminance());
                RestoreDC(hdc, saved);

                static std::atomic<int> drawLogCount{0};
                if (drawLogCount.fetch_add(1) < 20)
                {
                    Logging::Info(L"DRAW hdc=" + PathUtils::Utf8ToWide(std::to_string(
                        reinterpret_cast<uintptr_t>(hdc))) + L" image=" + imagePath);
                }
            }
            catch (...)
            {
                Logging::Error(L"Background draw failed.");
            }
        }

        HWND WINAPI HookCreateWindowExW(
            DWORD exStyle,
            LPCWSTR className,
            LPCWSTR windowName,
            DWORD style,
            int x,
            int y,
            int width,
            int height,
            HWND parent,
            HMENU menu,
            HINSTANCE instance,
            LPVOID parameter)
        {
            const HWND hwnd = g_originalCreateWindowExW(
                exStyle, className, windowName, style, x, y, width, height,
                parent, menu, instance, parameter);

            if (hwnd != nullptr && PathUtils::EqualsIgnoreCase(
                    PathUtils::GetWindowClassName(hwnd), L"DirectUIHWND"))
            {
                const HWND view = GetParent(hwnd);
                if (view != nullptr && PathUtils::EqualsIgnoreCase(
                        PathUtils::GetWindowClassName(view), L"SHELLDLL_DefView"))
                {
                    const HWND shell = GetParent(view);
                    const auto shellClass = PathUtils::GetWindowClassName(shell);
                    if (PathUtils::EqualsIgnoreCase(shellClass, L"ShellTabWindowClass") ||
                        PathUtils::EqualsIgnoreCase(shellClass, L"#32770"))
                    {
                        TrackWindow(hwnd);
                    }
                }
            }

            return hwnd;
        }

        BOOL WINAPI HookDestroyWindow(HWND hwnd)
        {
            UntrackWindow(hwnd);
            return g_originalDestroyWindow(hwnd);
        }

        HDC WINAPI HookBeginPaint(HWND hwnd, LPPAINTSTRUCT paint)
        {
            const HDC hdc = g_originalBeginPaint(hwnd, paint);
            if (hdc != nullptr)
            {
                g_duiDrewForHdc = false;
                std::lock_guard lock(g_stateMutex);
                const auto thread = g_windows.find(GetCurrentThreadId());
                if (thread != g_windows.end())
                {
                    const auto window = thread->second.find(hwnd);
                    if (window != thread->second.end())
                    {
                        window->second.hdc = hdc;
                    }
                }
            }

            return hdc;
        }

        int WINAPI HookFillRect(HDC hdc, const RECT* rect, HBRUSH brush)
        {
            const int result = g_originalFillRect(hdc, rect, brush);
            if (!g_duiAvailable.load())
            {
                DrawBackground(hdc, rect);
            }
            return result;
        }

        HDC WINAPI HookCreateCompatibleDC(HDC hdc)
        {
            const HDC result = g_originalCreateCompatibleDC(hdc);
            if (result != nullptr)
            {
                const HWND fromDc = WindowFromDC(hdc);
                if (fromDc != nullptr)
                {
                    std::lock_guard lock(g_stateMutex);
                    const auto thread = g_windows.find(GetCurrentThreadId());
                    if (thread != g_windows.end())
                    {
                        const auto window = thread->second.find(fromDc);
                        if (window != thread->second.end())
                        {
                            window->second.hdc = result;
                        }
                    }
                }
            }

            return result;
        }
    }

    bool Install()
    {
        if (g_installed.exchange(true))
        {
            return true;
        }

        if (MH_Initialize() != MH_OK)
        {
            g_installed = false;
            Logging::Error(L"MinHook initialization failed.");
            return false;
        }

        const bool created =
            MH_CreateHook(
                reinterpret_cast<LPVOID>(&CreateWindowExW),
                reinterpret_cast<LPVOID>(&HookCreateWindowExW),
                reinterpret_cast<LPVOID*>(&g_originalCreateWindowExW)) == MH_OK &&
            MH_CreateHook(
                reinterpret_cast<LPVOID>(&DestroyWindow),
                reinterpret_cast<LPVOID>(&HookDestroyWindow),
                reinterpret_cast<LPVOID*>(&g_originalDestroyWindow)) == MH_OK &&
            MH_CreateHook(
                reinterpret_cast<LPVOID>(&BeginPaint),
                reinterpret_cast<LPVOID>(&HookBeginPaint),
                reinterpret_cast<LPVOID*>(&g_originalBeginPaint)) == MH_OK &&
            MH_CreateHook(
                reinterpret_cast<LPVOID>(&FillRect),
                reinterpret_cast<LPVOID>(&HookFillRect),
                reinterpret_cast<LPVOID*>(&g_originalFillRect)) == MH_OK &&
            MH_CreateHook(
                reinterpret_cast<LPVOID>(&CreateCompatibleDC),
                reinterpret_cast<LPVOID>(&HookCreateCompatibleDC),
                reinterpret_cast<LPVOID*>(&g_originalCreateCompatibleDC)) == MH_OK;

        if (created)
        {
            g_duiAvailable = InstallDuiBackground();
            if (!g_duiAvailable.load())
            {
                Logging::Error(L"DUI background hook installation failed; using overlay fallback.");
            }
        }

        if (!created || MH_EnableHook(MH_ALL_HOOKS) != MH_OK)
        {
            MH_Uninitialize();
            g_installed = false;
            Logging::Error(L"MinHook hook creation or enable failed.");
            return false;
        }

        Logging::Info(L"Hooks installed.");
        return true;
    }

    void Uninstall()
    {
        if (!g_installed.exchange(false))
        {
            return;
        }

        MH_DisableHook(MH_ALL_HOOKS);
        MH_Uninitialize();
        Logging::Info(L"Hooks removed.");
    }

    void Clear()
    {
        std::lock_guard lock(g_stateMutex);
        g_windows.clear();
        g_locations.clear();
    }

    void SetLocationForThread(DWORD threadId, const std::wstring& location)
    {
        std::vector<HWND> windows;
        {
            std::lock_guard lock(g_stateMutex);
            g_locations[threadId] = location;

            const auto thread = g_windows.find(threadId);
            if (thread != g_windows.end())
            {
                windows.reserve(thread->second.size());
                for (const auto& [hwnd, state] : thread->second)
                {
                    (void)state;
                    windows.push_back(hwnd);
                }
            }
        }

        for (const HWND hwnd : windows)
        {
            InvalidateRect(hwnd, nullptr, TRUE);
        }
    }

    void InvalidateAll()
    {
        std::vector<HWND> windows;
        {
            std::lock_guard lock(g_stateMutex);
            for (const auto& [threadId, threadWindows] : g_windows)
            {
                (void)threadId;
                for (const auto& [hwnd, state] : threadWindows)
                {
                    (void)state;
                    windows.push_back(hwnd);
                }
            }
        }

        for (const HWND hwnd : windows)
        {
            InvalidateRect(hwnd, nullptr, TRUE);
        }
    }
}
