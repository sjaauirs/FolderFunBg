#include "AppState.h"

#include "Hooks.h"
#include "Logging.h"
#include "PathUtils.h"

using namespace Gdiplus;

namespace FolderFunBg
{
    ConfigWatcher::ConfigWatcher(std::function<void()> callback)
        : callback_(std::move(callback))
    {
        stopEvent_ = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    }

    ConfigWatcher::~ConfigWatcher()
    {
        Stop();
        if (stopEvent_ != nullptr)
        {
            CloseHandle(stopEvent_);
        }
    }

    void ConfigWatcher::Start()
    {
        if (thread_.joinable())
        {
            return;
        }

        const auto directory = PathUtils::GetAppDataDirectory();
        if (directory.empty())
        {
            Logging::Error(L"Config watcher: app data directory is unavailable.");
            return;
        }

        std::error_code error;
        std::filesystem::create_directories(directory, error);

        changeHandle_ = FindFirstChangeNotificationW(
            directory.c_str(),
            FALSE,
            FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_SIZE);

        if (changeHandle_ == INVALID_HANDLE_VALUE)
        {
            changeHandle_ = nullptr;
            Logging::Error(L"Config watcher: FindFirstChangeNotification failed.");
        }

        thread_ = std::thread(&ConfigWatcher::Run, this);
    }

    void ConfigWatcher::Stop()
    {
        if (!thread_.joinable())
        {
            if (changeHandle_ != nullptr)
            {
                FindCloseChangeNotification(changeHandle_);
                changeHandle_ = nullptr;
            }
            return;
        }

        if (stopEvent_ != nullptr)
        {
            SetEvent(stopEvent_);
        }

        thread_.join();

        if (changeHandle_ != nullptr)
        {
            FindCloseChangeNotification(changeHandle_);
            changeHandle_ = nullptr;
        }
    }

    void ConfigWatcher::Run()
    {
        HANDLE handles[2] = {stopEvent_, changeHandle_};
        const DWORD count = changeHandle_ != nullptr ? 2 : 1;

        for (;;)
        {
            const DWORD wait = WaitForMultipleObjects(count, handles, FALSE, INFINITE);
            if (wait == WAIT_OBJECT_0)
            {
                break;
            }

            if (wait == WAIT_OBJECT_0 + 1)
            {
                Sleep(150);

                try
                {
                    if (callback_)
                    {
                        callback_();
                    }
                }
                catch (...)
                {
                    Logging::Error(L"Config watcher callback failed.");
                }

                if (!FindNextChangeNotification(changeHandle_))
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
    }

    AppState& AppState::Instance()
    {
        static AppState instance;
        return instance;
    }

    void AppState::Start()
    {
        std::lock_guard lock(lifecycleMutex_);

        if (started_)
        {
            return;
        }

        GdiplusStartupInput input;
        if (GdiplusStartup(&gdiplusToken_, &input, nullptr) == Gdiplus::Ok)
        {
            gdiplusStarted_ = true;
        }
        else
        {
            Logging::Error(L"GDI+ initialization failed; backgrounds are disabled.");
        }

        ReloadConfig();

        watcher_ = std::make_unique<ConfigWatcher>([this]()
        {
            ReloadConfig();
            Hooks::InvalidateAll();
        });
        watcher_->Start();

        hooksReady_ = Hooks::Install();
        if (!hooksReady_)
        {
            Logging::Error(L"Explorer hook installation failed; will retry on navigation.");
        }

        started_ = true;
        Logging::Info(L"FolderFunBg hook started.");
    }

    void AppState::Stop()
    {
        std::lock_guard lock(lifecycleMutex_);

        if (!started_)
        {
            return;
        }

        if (watcher_ != nullptr)
        {
            watcher_->Stop();
            watcher_.reset();
        }

        if (hooksReady_)
        {
            Hooks::Uninstall();
            hooksReady_ = false;
        }

        Hooks::Clear();
        imageCache_.Clear();

        {
            std::unique_lock configLock(configMutex_);
            config_.reset();
        }

        if (gdiplusStarted_)
        {
            GdiplusShutdown(gdiplusToken_);
            gdiplusStarted_ = false;
            gdiplusToken_ = 0;
        }

        started_ = false;
        Logging::Info(L"FolderFunBg hook stopped.");
    }

    std::shared_ptr<const Config> AppState::GetConfig() const
    {
        std::shared_lock lock(configMutex_);
        return config_;
    }

    void AppState::EnsureHooks()
    {
        std::lock_guard lock(lifecycleMutex_);

        if (!started_ || hooksReady_)
        {
            return;
        }

        hooksReady_ = Hooks::Install();
        if (hooksReady_)
        {
            Logging::Info(L"Hooks installed after retry.");
        }
    }

    void AppState::ReloadConfig()
    {
        const auto path = PathUtils::GetConfigPath();
        if (path.empty())
        {
            Logging::Error(L"Config path is unavailable.");
            return;
        }

        try
        {
            auto loaded = Config::LoadFromFile(path);
            {
                std::unique_lock lock(configMutex_);
                config_ = std::move(loaded);
            }

            imageCache_.Clear();
            Logging::Info(L"Config loaded.");
        }
        catch (const std::exception& exception)
        {
            Logging::Error(L"Config load failed: " + PathUtils::Utf8ToWide(exception.what()));
        }
    }
}
