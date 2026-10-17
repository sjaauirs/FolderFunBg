#pragma once

#include "framework.h"

namespace FolderFunBg::PathUtils
{
    std::wstring GetModulePath(HMODULE module);
    std::wstring GetDllDirectory(HMODULE module);
    std::wstring GetLocalAppData();
    std::wstring GetAppDataDirectory();
    std::wstring GetConfigPath();
    std::wstring GetImagesRoot();
    std::wstring GetLogPath();
    std::wstring ExpandEnvironment(const std::wstring& value);
    std::wstring GetWindowClassName(HWND hwnd);
    std::wstring NormalizePath(const std::wstring& path);
    std::wstring NormalizeClsid(const std::wstring& value);
    bool IsUnderPath(const std::wstring& path, const std::wstring& parent);
    bool EqualsIgnoreCase(const std::wstring& left, const std::wstring& right);
    bool StartsWithIgnoreCase(const std::wstring& value, const std::wstring& prefix);
    bool EndsWithIgnoreCase(const std::wstring& value, const std::wstring& suffix);
    std::wstring ToLowerInvariant(const std::wstring& value);
    bool IsSafeImageFileName(const std::wstring& fileName);
    bool FileExists(const std::wstring& path);
    bool HasImageSignature(const std::wstring& path);
    std::wstring JoinPath(const std::wstring& directory, const std::wstring& name);
    std::string WideToUtf8(const std::wstring& value);
    std::wstring Utf8ToWide(const std::string& value);
}
