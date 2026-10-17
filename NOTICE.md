# Third-party notices

FolderFunBg bundles the following third-party components:

## MinHook

- Upstream: https://github.com/TsudaKageyu/minhook
- License: BSD 2-Clause
- Copyright (C) 2009-2017 Tsuda Kageyu
- License text: `third_party/minhook/LICENSE.txt`

## nlohmann/json

- Upstream: https://github.com/nlohmann/json
- License: MIT
- Copyright (c) 2013-2022 Niels Lohmann
- License text: `third_party/nlohmann/LICENSE.MIT`

## Technique reference

The in-Explorer painting approach was studied from:

- Maplespe/explorerTool: https://github.com/Maplespe/explorerTool
- License: MIT

FolderFunBg's hook implementation is a clean reimplementation with a different
config format, UI, error handling, and registration lifecycle. No source from
that project is copied into FolderFunBg; MinHook is vendored from its own
upstream repository under its own license.
