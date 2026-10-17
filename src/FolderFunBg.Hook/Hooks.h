#pragma once

#include "framework.h"

namespace FolderFunBg::Hooks
{
    bool Install();
    void Uninstall();
    void Clear();

    void SetLocationForThread(DWORD threadId, const std::wstring& location);
    void InvalidateAll();
}
