#include "PathUtils.h"

#include <knownfolders.h>

namespace FolderFunBg::PathUtils
{
    std::wstring GetModulePath(HMODULE module)
    {
        std::wstring buffer(MAX_PATH, L'\0');
        for (;;)
        {
            const DWORD length = GetModuleFileNameW(
                module, buffer.data(), static_cast<DWORD>(buffer.size()));
            if (length == 0)
            {
                return {};
            }

            if (length < buffer.size() - 1)
            {
                buffer.resize(length);
                return buffer;
            }

            buffer.resize(buffer.size() * 2);
        }
    }

    std::wstring GetDllDirectory(HMODULE module)
    {
        auto path = GetModulePath(module);
        const auto separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? std::wstring{} : path.substr(0, separator);
    }

    std::wstring GetLocalAppData()
    {
        PWSTR knownPath = nullptr;
        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_CREATE, nullptr, &knownPath)) &&
            knownPath != nullptr)
        {
            std::wstring result(knownPath);
            CoTaskMemFree(knownPath);
            return result;
        }

        wchar_t buffer[MAX_PATH] = {};
        const DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", buffer, MAX_PATH);
        if (length > 0 && length < MAX_PATH)
        {
            return std::wstring(buffer, length);
        }

        return {};
    }

    std::wstring GetAppDataDirectory()
    {
        const auto localAppData = GetLocalAppData();
        return localAppData.empty() ? std::wstring{} : JoinPath(localAppData, L"FolderFunBg");
    }

    std::wstring GetConfigPath()
    {
        const auto directory = GetAppDataDirectory();
        return directory.empty() ? std::wstring{} : JoinPath(directory, L"config.json");
    }

    std::wstring GetImagesRoot()
    {
        const auto directory = GetAppDataDirectory();
        return directory.empty() ? std::wstring{} : JoinPath(directory, L"Images");
    }

    std::wstring GetLogPath()
    {
        const auto directory = GetAppDataDirectory();
        return directory.empty() ? std::wstring{} : JoinPath(directory, L"Logs\\FolderFunBg.log");
    }

    std::wstring ExpandEnvironment(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const DWORD required = ExpandEnvironmentStringsW(value.c_str(), nullptr, 0);
        if (required == 0)
        {
            return value;
        }

        std::wstring expanded(required, L'\0');
        const DWORD written =
            ExpandEnvironmentStringsW(value.c_str(), expanded.data(), required);
        if (written == 0 || written > required)
        {
            return value;
        }

        expanded.resize(written - 1);
        return expanded;
    }

    std::wstring GetWindowClassName(HWND hwnd)
    {
        if (hwnd == nullptr)
        {
            return {};
        }

        wchar_t buffer[256] = {};
        const int length = GetClassNameW(hwnd, buffer, static_cast<int>(std::size(buffer)));
        return length > 0 ? std::wstring(buffer, static_cast<size_t>(length)) : std::wstring{};
    }

    std::wstring NormalizePath(const std::wstring& path)
    {
        if (path.empty())
        {
            return {};
        }

        const DWORD required = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
        if (required == 0)
        {
            return {};
        }

        std::wstring normalized(required, L'\0');
        const DWORD written =
            GetFullPathNameW(path.c_str(), required, normalized.data(), nullptr);
        if (written == 0 || written >= required)
        {
            return {};
        }

        normalized.resize(written);
        while (normalized.size() > 3 &&
               (normalized.back() == L'\\' || normalized.back() == L'/'))
        {
            normalized.pop_back();
        }

        return normalized;
    }

    std::wstring NormalizeClsid(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        std::wstring text = value;
        while (!text.empty() && iswspace(text.front()))
        {
            text.erase(text.begin());
        }
        while (!text.empty() && iswspace(text.back()))
        {
            text.pop_back();
        }

        if (text.rfind(L"::", 0) == 0)
        {
            text.erase(0, 2);
        }

        CLSID clsid{};
        if (FAILED(CLSIDFromString(text.c_str(), &clsid)))
        {
            return {};
        }

        wchar_t buffer[64] = {};
        if (StringFromGUID2(clsid, buffer, static_cast<int>(std::size(buffer))) <= 0)
        {
            return {};
        }

        return std::wstring(L"::") + buffer;
    }

    bool IsUnderPath(const std::wstring& path, const std::wstring& parent)
    {
        if (EqualsIgnoreCase(path, parent))
        {
            return true;
        }

        if (parent.empty())
        {
            return false;
        }

        if (parent.back() == L'\\' || parent.back() == L'/')
        {
            return StartsWithIgnoreCase(path, parent);
        }

        return StartsWithIgnoreCase(path, parent + L"\\");
    }

    bool EqualsIgnoreCase(const std::wstring& left, const std::wstring& right)
    {
        return _wcsicmp(left.c_str(), right.c_str()) == 0;
    }

    bool StartsWithIgnoreCase(const std::wstring& value, const std::wstring& prefix)
    {
        return value.size() >= prefix.size() &&
               _wcsnicmp(value.c_str(), prefix.c_str(), prefix.size()) == 0;
    }

    bool EndsWithIgnoreCase(const std::wstring& value, const std::wstring& suffix)
    {
        return value.size() >= suffix.size() &&
               _wcsicmp(value.c_str() + value.size() - suffix.size(), suffix.c_str()) == 0;
    }

    std::wstring ToLowerInvariant(const std::wstring& value)
    {
        std::wstring result = value;
        std::transform(result.begin(), result.end(), result.begin(), [](wchar_t character)
        {
            return static_cast<wchar_t>(towlower(character));
        });
        return result;
    }

    bool IsSafeImageFileName(const std::wstring& fileName)
    {
        if (fileName.empty() || fileName == L"." || fileName == L"..")
        {
            return false;
        }

        if (fileName.find_first_of(L"\\/:<>\"|?*") != std::wstring::npos)
        {
            return false;
        }

        for (const wchar_t character : fileName)
        {
            if (character < 0x20)
            {
                return false;
            }
        }

        const auto extension = std::filesystem::path(fileName).extension().wstring();
        return EqualsIgnoreCase(extension, L".png") ||
               EqualsIgnoreCase(extension, L".jpg") ||
               EqualsIgnoreCase(extension, L".jpeg");
    }

    bool FileExists(const std::wstring& path)
    {
        const DWORD attributes = GetFileAttributesW(path.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES &&
               (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    bool HasImageSignature(const std::wstring& path)
    {
        std::ifstream stream(std::filesystem::path(path), std::ios::binary);
        if (!stream)
        {
            return false;
        }

        unsigned char header[8] = {};
        stream.read(reinterpret_cast<char*>(header), sizeof(header));
        const auto read = static_cast<size_t>(stream.gcount());

        constexpr unsigned char png[8] = {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A};
        if (read >= 8 && std::equal(std::begin(png), std::end(png), header))
        {
            return true;
        }

        return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    }

    std::wstring JoinPath(const std::wstring& directory, const std::wstring& name)
    {
        if (directory.empty())
        {
            return name;
        }

        if (name.empty())
        {
            return directory;
        }

        if (directory.back() == L'\\' || directory.back() == L'/')
        {
            return directory + name;
        }

        return directory + L"\\" + name;
    }

    std::string WideToUtf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int required = WideCharToMultiByte(
            CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
            nullptr, 0, nullptr, nullptr);
        if (required <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(required), '\0');
        WideCharToMultiByte(
            CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
            result.data(), required, nullptr, nullptr);
        return result;
    }

    std::wstring Utf8ToWide(const std::string& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int required = MultiByteToWideChar(
            CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()),
            nullptr, 0);
        if (required <= 0)
        {
            return {};
        }

        std::wstring result(static_cast<size_t>(required), L'\0');
        MultiByteToWideChar(
            CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()),
            result.data(), required);
        return result;
    }
}
