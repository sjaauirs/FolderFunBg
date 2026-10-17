#include "framework.h"

#include "AppState.h"
#include "ComBho.h"
#include "Logging.h"
#include "PathUtils.h"

using namespace FolderFunBg;

HMODULE g_module = nullptr;

namespace
{
    bool ShouldLoad()
    {
        const auto modulePath = PathUtils::GetModulePath(nullptr);
        return PathUtils::EndsWithIgnoreCase(modulePath, L"explorer.exe") ||
               PathUtils::EndsWithIgnoreCase(modulePath, L"regsvr32.exe");
    }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        if (g_module != nullptr)
        {
            return TRUE;
        }

        g_module = module;
        DisableThreadLibraryCalls(module);
        return ShouldLoad() ? TRUE : FALSE;
    }

    if (reason == DLL_PROCESS_DETACH)
    {
        if (g_module == module)
        {
            if (reserved == nullptr)
            {
                FolderFunBg::AppState::Instance().Stop();
            }
            g_module = nullptr;
        }
    }

    return TRUE;
}
