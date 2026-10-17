param(
    [switch]$SkipTests,
    [switch]$DebugBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$configuration = if ($DebugBuild) { "Debug" } else { "Release" }

Push-Location $root
try {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Install Visual Studio 2022 with the C++ desktop workload."
    }

    $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vsPath) {
        throw "Visual Studio with the C++ desktop workload was not found."
    }

    $vcvars = Join-Path $vsPath "VC\Auxiliary\Build\vcvarsall.bat"
    $msbuild = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
    $nativeProject = Join-Path $root "src\FolderFunBg.Hook\FolderFunBg.Hook.vcxproj"
    $env:PATH = (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer") + ";" + $env:PATH

    Write-Host "Building native hook ($configuration|x64)..."
    & cmd.exe /c "call `"$vcvars`" x64 >nul && `"$msbuild`" `"$nativeProject`" /p:Configuration=$configuration /p:Platform=x64 /m /nologo /v:minimal"
    if ($LASTEXITCODE -ne 0) {
        throw "Native build failed."
    }

    Write-Host "Building C# core, tests, and settings app..."
    dotnet build (Join-Path $root "FolderFunBg.sln") -c $configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "C# build failed."
    }

    if (-not $SkipTests) {
        Write-Host "Running core tests..."
        dotnet test (Join-Path $root "tests\FolderFunBg.Core.Tests\FolderFunBg.Core.Tests.csproj") -c $configuration --no-build --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed."
        }
    }

    $settingsOutput = Join-Path $root "src\FolderFunBg.Settings\bin\x64\$configuration\net9.0-windows10.0.19041.0\win-x64"
    $stage = Join-Path $root "install"
    if (Test-Path $stage) {
        Remove-Item $stage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item (Join-Path $root "bin\x64\$configuration\FolderFunBg.Hook.dll") $stage -Force
    Copy-Item (Join-Path $root "installer\*.ps1") $stage -Force

    Write-Host ""
    Write-Host "Build complete."
    Write-Host "  Native hook : bin\x64\$configuration\FolderFunBg.Hook.dll"
    Write-Host "  Settings app: $settingsOutput\FolderFunBg.Settings.exe"
    Write-Host "  Staged files: $stage"
}
finally {
    Pop-Location
}
