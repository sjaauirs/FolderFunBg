#include "Logging.h"

#include "PathUtils.h"

namespace FolderFunBg::Logging
{
    namespace
    {
        constexpr long long MaxLogBytes = 1024 * 1024;
        std::mutex g_logMutex;

        void AppendToFile(const wchar_t* level, const std::wstring& message)
        {
            std::lock_guard lock(g_logMutex);

            const auto logPath = PathUtils::GetLogPath();
            if (logPath.empty())
            {
                return;
            }

            const auto directory = PathUtils::GetDllDirectory(nullptr);
            (void)directory;

            const auto parent = std::filesystem::path(logPath).parent_path();
            std::error_code error;
            std::filesystem::create_directories(parent, error);
            if (error)
            {
                return;
            }

            std::error_code sizeError;
            const auto size = std::filesystem::file_size(logPath, sizeError);
            if (!sizeError && size > static_cast<std::uintmax_t>(MaxLogBytes))
            {
                std::filesystem::remove(logPath, error);
            }

            const auto utf8 = PathUtils::WideToUtf8(std::wstring(level) + L" " + message + L"\r\n");
            HANDLE file = CreateFileW(
                logPath.c_str(),
                FILE_APPEND_DATA,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_ALWAYS,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return;
            }

            DWORD written = 0;
            WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
            CloseHandle(file);
        }

        void Emit(const wchar_t* level, const std::wstring& message)
        {
            const std::wstring line = std::wstring(L"[FolderFunBg] ") + level + L" " + message + L"\n";
            OutputDebugStringW(line.c_str());
        }
    }

    void Debug(const std::wstring& message)
    {
        Emit(L"DEBUG", message);
    }

    void Info(const std::wstring& message)
    {
        Emit(L"INFO", message);
        AppendToFile(L"INFO", message);
    }

    void Error(const std::wstring& message)
    {
        Emit(L"ERROR", message);
        AppendToFile(L"ERROR", message);
    }
}
