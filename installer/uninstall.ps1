param(
    [switch]$RestartExplorer
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`""
    )
    if ($RestartExplorer) { $arguments += "-RestartExplorer" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments
    exit 0
}

$destination = Join-Path $env:ProgramFiles "FolderFunBg"
$target = Join-Path $destination "FolderFunBg.Hook.dll"
if (Test-Path -LiteralPath $target) {
    & regsvr32.exe /s /u $target
}

$clsid = "{80BDC533-D192-445A-A1D9-F91310021919}"
Remove-Item "HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "HKLM:\SOFTWARE\Classes\Drive\shellex\FolderExtensions\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "HKLM:\SOFTWARE\Classes\CLSID\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $destination -Recurse -Force -ErrorAction SilentlyContinue

if ($RestartExplorer) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Start-Process explorer.exe
}

Write-Host "FolderFunBg uninstalled."
