@echo off
setlocal enabledelayedexpansion
title SE test plugin installer

REM ============================================================
REM  Space Engineers Pulsar test plugin installer
REM  - finds Pulsar (%%AppData%%\Pulsar, or override below/arg)
REM  - copies the plugin files into every installed Pulsar edition
REM  - enables the plugin in the active profile (Current.xml)
REM  - safe to run again (does not duplicate entries)
REM ============================================================

set "SCRIPT_DIR=%~dp0"

REM Pulsar location: 1) script argument, 2) PULSAR_DIR env var, 3) default
set "PULSAR=%~1"
if not defined PULSAR set "PULSAR=%PULSAR_DIR%"
if not defined PULSAR set "PULSAR=%AppData%\Pulsar"

echo.
echo === SE test plugin installer ===
echo Pulsar folder: %PULSAR%
echo.

if not exist "%PULSAR%" (
    echo ERROR: Pulsar folder not found.
    echo Install Pulsar first, or pass its folder as argument:
    echo     %~nx0 "C:\path\to\Pulsar"
    echo.
    pause
    exit /b 1
)

if not exist "%SCRIPT_DIR%Plugin" (
    echo ERROR: Plugin folder not found next to this script:
    echo     %SCRIPT_DIR%Plugin
    echo The package seems incomplete - please re-download it.
    echo.
    pause
    exit /b 1
)

set "INSTALLED=0"

for %%E in (Legacy Interim) do (
    call :install_edition "%%E"
    if !ERRORLEVEL! GEQ 2 (
        echo.
        echo Installation FAILED. See messages above.
        pause
        exit /b 2
    )
)

echo.
if "%INSTALLED%"=="0" (
    echo WARNING: no Pulsar edition was found ^(no Profiles\Current.xml^).
    echo Is Pulsar installed and started at least once?
) else (
    echo Done. Start the game through Pulsar and test the plugin.
    echo To remove it later, run Uninstall-TestPlugin.bat.
)
echo.
pause
exit /b 0

REM ------------------------------------------------------------
:install_edition
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

set "DEST=%PULSAR%\%EDITION%\Local"
if not exist "%DEST%" mkdir "%DEST%"

echo [Pulsar %EDITION%] copying plugin files...
copy /y "%SCRIPT_DIR%Plugin\%EDITION%\*" "%DEST%\" >nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: could not copy to "%DEST%".
    echo Make sure the game is CLOSED and try again.
    exit /b 2
)

set "ENABLED_ANY=0"
for %%D in ("%SCRIPT_DIR%Plugin\%EDITION%\*.dll") do (
    call :enable_dll "%PROFILE%" "%%~nxD"
    if !ERRORLEVEL! GEQ 2 exit /b 2
    set "ENABLED_ANY=1"
)

if "%ENABLED_ANY%"=="1" set "INSTALLED=1"
exit /b 0

REM ------------------------------------------------------------
REM %1 = profile xml, %2 = dll file name
:enable_dll
set "PROFILE=%~1"
set "DLLNAME=%~2"

REM one-time backup of the profile (keeps the state before our first change)
if not exist "%PROFILE%.bak-testplugin" (
    copy /y "%PROFILE%" "%PROFILE%.bak-testplugin" >nul
)

REM escape single quotes for the PowerShell string literal
set "PSF=%PROFILE:'=''%"
set "PSD=%DLLNAME:'=''%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%PSF%';$d='%PSD%';$nl=[string][char]13+[string][char]10;$t=[IO.File]::ReadAllText($f);$e='<string>'+$d+'</string>';if($t.Contains($e)){exit 1};if($t.Contains('<Local>')){$t=$t.Replace('<Local>','<Local>'+$nl+'    '+$e)}elseif($t.Contains('<Local />')){$t=$t.Replace('<Local />','<Local>'+$nl+'    '+$e+$nl+'  </Local>')}else{exit 2};[IO.File]::WriteAllText($f,$t,(New-Object Text.UTF8Encoding($false)));exit 0"

if %ERRORLEVEL% EQU 0 (
    echo [Pulsar %EDITION%] enabled %DLLNAME% in the active profile.
    exit /b 0
)
if %ERRORLEVEL% EQU 1 (
    echo [Pulsar %EDITION%] %DLLNAME% was already enabled, nothing to do.
    exit /b 0
)
echo [Pulsar %EDITION%] ERROR: could not edit "%PROFILE%" - no ^<Local^> section found.
exit /b 2
