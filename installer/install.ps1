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

$logPath = Join-Path $env:TEMP "FolderFunBg-install.log"
Start-Transcript -Path $logPath -Force | Out-Null

try {
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

    # Explorer keeps the previous hook loaded, and Windows refuses to overwrite
    # a loaded image. Stop Explorer first; the shell is started again below.
    # Windows may auto-restart Explorer, so retry the copy while stopping any
    # process that grabs the file again.
    if ($RestartExplorer) {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 700
    }

    $copied = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try {
            Copy-Item -LiteralPath $DllPath -Destination $target -Force -ErrorAction Stop
            $copied = $true
            break
        }
        catch {
            Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $copied) {
        throw "Could not replace $target. Run the installer with -RestartExplorer so Explorer releases the loaded hook."
    }

    & regsvr32.exe /s $target
    if ($LASTEXITCODE -ne 0) {
        throw "regsvr32 failed with exit code $LASTEXITCODE."
    }

    if ($RestartExplorer) {
        # Stop any shell that auto-restarted after the copy, then start a fresh
        # one so it loads the newly registered hook.
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Start-Process explorer.exe
    }

    Write-Host "FolderFunBg installed to $destination."
}
finally {
    Stop-Transcript | Out-Null
}
