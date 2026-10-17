param(
    [string]$DllPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "FolderFunBg.Hook.dll"),
    [switch]$RestartExplorer,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`"",
        "-DllPath", "`"$DllPath`""
    )
    if ($RestartExplorer) { $arguments += "-RestartExplorer" }
    if ($Force) { $arguments += "-Force" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments
    exit 0
}

$DllPath = [System.IO.Path]::GetFullPath($DllPath)
if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "DLL not found: $DllPath"
}

$legacyClsid = "{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}"
$legacyKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\$legacyClsid"
if ((Test-Path $legacyKey) -and -not $Force) {
    throw "ExplorerBgTool is still registered. Run its Uninstall.cmd first, or rerun with -Force (two hook stacks can crash Explorer)."
}

$destination = Join-Path $env:ProgramFiles "FolderFunBg"
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$target = Join-Path $destination "FolderFunBg.Hook.dll"
Copy-Item -LiteralPath $DllPath -Destination $target -Force

& regsvr32.exe /s $target
if ($LASTEXITCODE -ne 0) {
    throw "regsvr32 failed with exit code $LASTEXITCODE."
}

if ($RestartExplorer) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Start-Process explorer.exe
}

Write-Host "FolderFunBg installed to $destination."
