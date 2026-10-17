#include "Config.h"

#include "PathUtils.h"

#include "../../third_party/nlohmann/json.hpp"

namespace FolderFunBg
{
    namespace
    {
        using json = nlohmann::json;

        std::wstring GetWString(const json& object, const char* key, const std::wstring& fallback = {})
        {
            const auto iterator = object.find(key);
            if (iterator == object.end() || !iterator->is_string())
            {
                return fallback;
            }

            const auto value = PathUtils::Utf8ToWide(iterator->get<std::string>());
            return value.empty() && !iterator->get<std::string>().empty() ? fallback : value;
        }

        std::optional<std::wstring> GetOptionalWString(const json& object, const char* key)
        {
            const auto iterator = object.find(key);
            if (iterator == object.end() || iterator->is_null())
            {
                return std::nullopt;
            }

            if (!iterator->is_string())
            {
                throw std::runtime_error(std::string(key) + " must be a string.");
            }

            return PathUtils::Utf8ToWide(iterator->get<std::string>());
        }

        std::optional<int> GetOptionalInt(const json& object, const char* key)
        {
            const auto iterator = object.find(key);
            if (iterator == object.end() || iterator->is_null())
            {
                return std::nullopt;
            }

            if (!iterator->is_number_integer())
            {
                throw std::runtime_error(std::string(key) + " must be an integer.");
            }

            return iterator->get<int>();
        }

        std::optional<bool> GetOptionalBool(const json& object, const char* key)
        {
            const auto iterator = object.find(key);
            if (iterator == object.end() || iterator->is_null())
            {
                return std::nullopt;
            }

            if (!iterator->is_boolean())
            {
                throw std::runtime_error(std::string(key) + " must be a boolean.");
            }

            return iterator->get<bool>();
        }

        bool GetBool(const json& object, const char* key, bool fallback)
        {
            const auto value = GetOptionalBool(object, key);
            return value.value_or(fallback);
        }

        Position ParsePosition(const std::wstring& value, bool& ok)
        {
            const auto lower = PathUtils::ToLowerInvariant(value);
            ok = true;

            if (lower == L"top-left" || lower == L"topleft")
            {
                return Position::TopLeft;
            }
            if (lower == L"top-right" || lower == L"topright")
            {
                return Position::TopRight;
            }
            if (lower == L"bottom-left" || lower == L"bottomleft")
            {
                return Position::BottomLeft;
            }
            if (lower == L"bottom-right" || lower == L"bottomright")
            {
                return Position::BottomRight;
            }
            if (lower == L"center")
            {
                return Position::Center;
            }
            if (lower == L"stretch")
            {
                return Position::Stretch;
            }
            if (lower == L"zoom-fill" || lower == L"zoomfill")
            {
                return Position::ZoomFill;
            }

            ok = false;
            return Position::BottomRight;
        }

        ReadabilityMode ParseReadabilityMode(const std::wstring& value, bool& ok)
        {
            const auto lower = PathUtils::ToLowerInvariant(value);
            ok = true;

            if (lower == L"auto")
            {
                return ReadabilityMode::Auto;
            }
            if (lower == L"none")
            {
                return ReadabilityMode::None;
            }
            if (lower == L"light")
            {
                return ReadabilityMode::Light;
            }
            if (lower == L"dark")
            {
                return ReadabilityMode::Dark;
            }

            ok = false;
            return ReadabilityMode::Auto;
        }

        ReadabilitySettings ParseReadability(
            const json& object,
            const ReadabilitySettings& fallback,
            bool& ok)
        {
            ReadabilitySettings result = fallback;
            ok = true;

            if (!object.is_object())
            {
                ok = false;
                return result;
            }

            if (const auto mode = GetOptionalWString(object, "mode"))
            {
                result.mode = ParseReadabilityMode(*mode, ok);
                if (!ok)
                {
                    return result;
                }
            }

            if (const auto strength = GetOptionalInt(object, "strength"))
            {
                if (*strength < 0 || *strength > 255)
                {
                    ok = false;
                    return result;
                }

                result.strength = *strength;
            }

            if (const auto gradient = GetOptionalBool(object, "gradient"))
            {
                result.gradient = *gradient;
            }

            return result;
        }

        std::wstring ExpandAndNormalizePath(const std::wstring& value)
        {
            const auto expanded = PathUtils::ExpandEnvironment(value);
            if (expanded.empty() || PathIsRelativeW(expanded.c_str()))
            {
                throw std::runtime_error("path must be absolute.");
            }

            const auto normalized = PathUtils::NormalizePath(expanded);
            if (normalized.empty())
            {
                throw std::runtime_error("path could not be normalized.");
            }

            return normalized;
        }

        std::wstring NormalizeClsidValue(const std::wstring& value)
        {
            const auto normalized = PathUtils::NormalizeClsid(value);
            if (normalized.empty())
            {
                throw std::runtime_error("clsid is not a valid GUID.");
            }

            return normalized;
        }

        void RequireSafeImage(const std::wstring& image, const char* field)
        {
            if (!PathUtils::IsSafeImageFileName(image))
            {
                throw std::runtime_error(std::string(field) +
                    " must be a file name ending in .png, .jpg, or .jpeg.");
            }
        }

        bool IsClsidLocation(const std::wstring& location)
        {
            const auto trimmed = PathUtils::ToLowerInvariant(location);
            return trimmed.rfind(L"::", 0) == 0 || (!trimmed.empty() && trimmed.front() == L'{');
        }

        std::vector<std::wstring> EnumerateImages(const std::wstring& root)
        {
            std::vector<std::wstring> files;
            if (root.empty())
            {
                return files;
            }

            for (const wchar_t* pattern : {L"*.png", L"*.jpg", L"*.jpeg"})
            {
                WIN32_FIND_DATAW data{};
                const auto search = PathUtils::JoinPath(root, pattern);
                HANDLE find = FindFirstFileW(search.c_str(), &data);
                if (find == INVALID_HANDLE_VALUE)
                {
                    continue;
                }

                do
                {
                    if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {
                        continue;
                    }

                    const std::wstring fileName = data.cFileName;
                    if (!PathUtils::IsSafeImageFileName(fileName))
                    {
                        continue;
                    }

                    const auto fullPath = PathUtils::JoinPath(root, fileName);
                    if (PathUtils::HasImageSignature(fullPath))
                    {
                        files.push_back(fileName);
                    }
                } while (FindNextFileW(find, &data));

                FindClose(find);
            }

            std::sort(files.begin(), files.end(), [](const std::wstring& left, const std::wstring& right)
            {
                return _wcsicmp(left.c_str(), right.c_str()) < 0;
            });
            files.erase(std::unique(files.begin(), files.end(), [](const std::wstring& left, const std::wstring& right)
            {
                return _wcsicmp(left.c_str(), right.c_str()) == 0;
            }), files.end());

            return files;
        }

        bool ReadLightTheme()
        {
            HKEY key = nullptr;
            if (RegOpenKeyExW(
                    HKEY_CURRENT_USER,
                    L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                    0,
                    KEY_READ,
                    &key) != ERROR_SUCCESS)
            {
                return true;
            }

            DWORD value = 1;
            DWORD size = sizeof(value);
            DWORD type = REG_DWORD;
            const auto status = RegQueryValueExW(
                key, L"AppsUseLightTheme", nullptr, &type,
                reinterpret_cast<LPBYTE>(&value), &size);
            RegCloseKey(key);

            return status != ERROR_SUCCESS || type != REG_DWORD || value != 0;
        }

        std::wstring DuplicateKey(const Rule& rule)
        {
            return rule.clsid.empty()
                ? L"P:" + PathUtils::ToLowerInvariant(rule.path)
                : L"C:" + PathUtils::ToLowerInvariant(rule.clsid);
        }
    }

    std::shared_ptr<const Config> Config::LoadFromFile(const std::wstring& path)
    {
        std::ifstream stream(std::filesystem::path(path), std::ios::binary);
        if (!stream)
        {
            throw std::runtime_error("config file does not exist.");
        }

        json document;
        try
        {
            document = json::parse(stream, nullptr, false);
        }
        catch (const json::exception& exception)
        {
            throw std::runtime_error(std::string("config JSON parse failed: ") + exception.what());
        }

        if (document.is_discarded() || !document.is_object())
        {
            throw std::runtime_error("config JSON is not an object.");
        }

        try
        {
            auto config = std::make_shared<Config>();

            if (const auto version = GetOptionalInt(document, "version"); version.value_or(1) < 1)
            {
                throw std::runtime_error("version must be at least 1.");
            }

            config->imagesRoot_ = ExpandAndNormalizePath(
                GetWString(document, "imagesRoot", L"%LOCALAPPDATA%\\FolderFunBg\\Images"));

            if (const auto defaults = document.find("defaults"); defaults != document.end())
            {
                if (!defaults->is_object())
                {
                    throw std::runtime_error("defaults must be an object.");
                }

                if (const auto image = GetOptionalWString(*defaults, "image"))
                {
                    RequireSafeImage(*image, "defaults.image");
                    config->defaults_.image = *image;
                }

                if (const auto position = GetOptionalWString(*defaults, "position"))
                {
                    bool ok = false;
                    config->defaults_.position = ParsePosition(*position, ok);
                    if (!ok)
                    {
                        throw std::runtime_error("defaults.position is not supported.");
                    }
                }

                if (const auto alpha = GetOptionalInt(*defaults, "alpha"))
                {
                    if (*alpha < 0 || *alpha > 255)
                    {
                        throw std::runtime_error("defaults.alpha must be between 0 and 255.");
                    }
                    config->defaults_.alpha = *alpha;
                }

                if (const auto random = GetOptionalBool(*defaults, "random"))
                {
                    config->defaults_.random = *random;
                }
            }

            if (const auto readability = document.find("readability"); readability != document.end())
            {
                bool ok = false;
                config->readability_ = ParseReadability(*readability, config->readability_, ok);
                if (!ok)
                {
                    throw std::runtime_error("readability settings are invalid.");
                }
            }

            if (const auto folders = document.find("folders"); folders != document.end())
            {
                if (!folders->is_array())
                {
                    throw std::runtime_error("folders must be an array.");
                }

                std::unordered_set<std::wstring> keys;
                for (const auto& item : *folders)
                {
                    if (!item.is_object())
                    {
                        throw std::runtime_error("each folder rule must be an object.");
                    }

                    Rule rule;
                    if (const auto rulePath = GetOptionalWString(item, "path");
                        rulePath && !rulePath->empty())
                    {
                        rule.path = ExpandAndNormalizePath(*rulePath);
                    }

                    if (const auto clsid = GetOptionalWString(item, "clsid");
                        clsid && !clsid->empty())
                    {
                        rule.clsid = NormalizeClsidValue(*clsid);
                    }

                    if (rule.path.empty() == rule.clsid.empty())
                    {
                        throw std::runtime_error("each folder rule must set exactly one of path or clsid.");
                    }

                    if (const auto image = GetOptionalWString(item, "image");
                        image && !image->empty())
                    {
                        RequireSafeImage(*image, "folders[].image");
                        rule.image = *image;
                    }

                    if (const auto position = GetOptionalWString(item, "position");
                        position && !position->empty())
                    {
                        bool ok = false;
                        rule.position = ParsePosition(*position, ok);
                        if (!ok)
                        {
                            throw std::runtime_error("folders[].position is not supported.");
                        }
                    }

                    if (const auto alpha = GetOptionalInt(item, "alpha"))
                    {
                        if (*alpha < 0 || *alpha > 255)
                        {
                            throw std::runtime_error("folders[].alpha must be between 0 and 255.");
                        }
                        rule.alpha = *alpha;
                    }

                    if (const auto random = GetOptionalBool(item, "random"))
                    {
                        rule.random = *random;
                    }

                    rule.recursive = GetBool(item, "recursive", false);
                    rule.enabled = GetBool(item, "enabled", true);

                    if (rule.recursive && !rule.clsid.empty())
                    {
                        throw std::runtime_error("a clsid rule cannot be recursive.");
                    }

                    if (const auto readability = item.find("readability"); readability != item.end())
                    {
                        bool ok = false;
                        rule.readability = ParseReadability(*readability, config->readability_, ok);
                        if (!ok)
                        {
                            throw std::runtime_error("folders[].readability settings are invalid.");
                        }
                    }

                    if (rule.enabled && !keys.insert(DuplicateKey(rule)).second)
                    {
                        throw std::runtime_error("duplicate enabled folder rule.");
                    }

                    config->folders_.push_back(std::move(rule));
                }
            }

            config->imageFiles_ = EnumerateImages(config->imagesRoot_);
            config->lightTheme_ = ReadLightTheme();
            return config;
        }
        catch (const json::exception& exception)
        {
            throw std::runtime_error(std::string("config JSON type error: ") + exception.what());
        }
    }

    ResolvedBackground Config::Resolve(const std::wstring& location) const
    {
        ResolvedBackground resolved;
        resolved.image = defaults_.image;
        resolved.position = defaults_.position;
        resolved.alpha = defaults_.alpha;
        resolved.random = defaults_.random;
        resolved.readability = readability_;

        if (location.empty())
        {
            return resolved;
        }

        const Rule* matched = nullptr;

        if (IsClsidLocation(location))
        {
            const auto clsid = PathUtils::NormalizeClsid(location);
            if (!clsid.empty())
            {
                for (const auto& rule : folders_)
                {
                    if (rule.enabled && !rule.clsid.empty() &&
                        PathUtils::EqualsIgnoreCase(rule.clsid, clsid))
                    {
                        matched = &rule;
                        break;
                    }
                }
            }
        }
        else
        {
            const auto path = PathUtils::NormalizePath(PathUtils::ExpandEnvironment(location));
            if (path.empty())
            {
                return resolved;
            }

            std::size_t recursiveLength = 0;
            for (const auto& rule : folders_)
            {
                if (!rule.enabled || rule.path.empty())
                {
                    continue;
                }

                if (PathUtils::EqualsIgnoreCase(rule.path, path))
                {
                    matched = &rule;
                    break;
                }

                if (rule.recursive &&
                    PathUtils::IsUnderPath(path, rule.path) &&
                    rule.path.size() > recursiveLength)
                {
                    matched = &rule;
                    recursiveLength = rule.path.size();
                }
            }
        }

        if (matched == nullptr)
        {
            return resolved;
        }

        resolved.image = matched->image.empty() ? defaults_.image : matched->image;
        resolved.position = matched->position.value_or(defaults_.position);
        resolved.alpha = std::clamp(matched->alpha.value_or(defaults_.alpha), 0, 255);
        resolved.random = matched->random.value_or(defaults_.random);
        resolved.readability = matched->readability.value_or(readability_);
        resolved.matchedRule = true;
        return resolved;
    }

    std::wstring Config::ImagePath(const std::wstring& fileName) const
    {
        if (fileName.empty() || !PathUtils::IsSafeImageFileName(fileName))
        {
            return {};
        }

        return PathUtils::JoinPath(imagesRoot_, fileName);
    }

    std::wstring Config::PickRandomImage() const
    {
        if (imageFiles_.empty())
        {
            return {};
        }

        thread_local std::mt19937 generator([]()
        {
            std::random_device device;
            return device() ^ static_cast<unsigned>(GetCurrentThreadId()) ^
                   static_cast<unsigned>(GetTickCount64());
        }());

        std::uniform_int_distribution<size_t> distribution(0, imageFiles_.size() - 1);
        return imageFiles_[distribution(generator)];
    }
}
