#pragma once

#include "framework.h"
#include "Config.h"
#include "ImageCache.h"

namespace FolderFunBg
{
    class ConfigWatcher
    {
    public:
        explicit ConfigWatcher(std::function<void()> callback);
        ~ConfigWatcher();

        ConfigWatcher(const ConfigWatcher&) = delete;
        ConfigWatcher& operator=(const ConfigWatcher&) = delete;

        void Start();
        void Stop();

    private:
        void Run();

        std::function<void()> callback_;
        HANDLE changeHandle_ = nullptr;
        HANDLE stopEvent_ = nullptr;
        std::thread thread_;
    };

    class AppState
    {
    public:
        static AppState& Instance();

        void Start();
        void Stop();

        std::shared_ptr<const Config> GetConfig() const;
        ImageCache& GetImageCache() { return imageCache_; }

        void EnsureHooks();
        void ReloadConfig();

    private:
        AppState() = default;
        ~AppState() = default;

        AppState(const AppState&) = delete;
        AppState& operator=(const AppState&) = delete;

        mutable std::shared_mutex configMutex_;
        std::shared_ptr<const Config> config_;
        ImageCache imageCache_;

        std::mutex lifecycleMutex_;
        std::unique_ptr<ConfigWatcher> watcher_;
        bool started_ = false;
        bool hooksReady_ = false;
        bool gdiplusStarted_ = false;
        ULONG_PTR gdiplusToken_ = 0;
    };
}
