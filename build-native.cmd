@echo off
setlocal
set "VS=C:\Program Files\Microsoft Visual Studio\2022\Community"
set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
call "%VS%\VC\Auxiliary\Build\vcvarsall.bat" x64
if errorlevel 1 exit /b 1
"%VS%\MSBuild\Current\Bin\MSBuild.exe" "%~dp0src\FolderFunBg.Hook\FolderFunBg.Hook.vcxproj" /p:Configuration=Release /p:Platform=x64 /m /nologo /v:minimal
exit /b %ERRORLEVEL%
