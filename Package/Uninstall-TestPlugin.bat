@echo off
setlocal enabledelayedexpansion
title SE test plugin uninstaller

REM ============================================================
REM  Removes the test plugin installed by Install-TestPlugin.bat:
REM  - disables it in the active profile (Current.xml)
REM  - deletes the plugin files from Pulsar's Local folder
REM  A profile backup (.bak-testplugin) is kept.
REM ============================================================

set "SCRIPT_DIR=%~dp0"

set "PULSAR=%~1"
if not defined PULSAR set "PULSAR=%PULSAR_DIR%"
if not defined PULSAR set "PULSAR=%AppData%\Pulsar"

echo.
echo === SE test plugin uninstaller ===
echo Pulsar folder: %PULSAR%
echo.

if not exist "%PULSAR%" (
    echo ERROR: Pulsar folder not found: %PULSAR%
    echo.
    pause
    exit /b 1
)

if not exist "%SCRIPT_DIR%Plugin" (
    echo ERROR: Plugin folder not found next to this script:
    echo     %SCRIPT_DIR%Plugin
    echo.
    pause
    exit /b 1
)

for %%E in (Legacy Interim) do call :uninstall_edition "%%E"

echo.
echo Done. You can delete the extracted package folder now.
echo.
pause
exit /b 0

REM ------------------------------------------------------------
:uninstall_edition
set "EDITION=%~1"
set "PROFILE=%PULSAR%\%EDITION%\Profiles\Current.xml"

if not exist "%PROFILE%" (
    echo [Pulsar %EDITION%] not installed here, skipping.
    exit /b 0
)
if not exist "%SCRIPT_DIR%Plugin\%EDITION%" (
    echo [Pulsar %EDITION%] this package has no %EDITION% build, skipping.
    exit /b 0
)

for %%D in ("%SCRIPT_DIR%Plugin\%EDITION%\*.dll") do (
    call :disable_dll "%PROFILE%" "%%~nxD"
    if exist "%PULSAR%\%EDITION%\Local\%%~nxD" del /q "%PULSAR%\%EDITION%\Local\%%~nxD"
    if exist "%PULSAR%\%EDITION%\Local\%%~nxD.xml" del /q "%PULSAR%\%EDITION%\Local\%%~nxD.xml"
)
exit /b 0

REM ------------------------------------------------------------
REM %1 = profile xml, %2 = dll file name
:disable_dll
set "PROFILE=%~1"
set "DLLNAME=%~2"

set "PSF=%PROFILE:'=''%"
set "PSD=%DLLNAME:'=''%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%PSF%';$d='%PSD%';$t=[IO.File]::ReadAllText($f);$t2=[regex]::Replace($t,'\r?\n\s*<string>'+[regex]::Escape($d)+'</string>','');if($t -eq $t2){exit 1};[IO.File]::WriteAllText($f,$t2,(New-Object Text.UTF8Encoding($false)));exit 0"

if %ERRORLEVEL% EQU 0 (
    echo [Pulsar %EDITION%] disabled %DLLNAME% in the active profile.
) else (
    echo [Pulsar %EDITION%] %DLLNAME% was not enabled, nothing to do.
)
exit /b 0
