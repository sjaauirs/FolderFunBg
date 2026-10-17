#pragma once

// Build-compatibility shim for the vendored MinHook sources, which expect
// ../framework.h relative to the minhook directory.
#include <sdkddkver.h>

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <windows.h>
