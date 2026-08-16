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

REM ------------------------------------------------------------
REM Detect the game's ACTUAL runtime from the newest game log.
REM Folder name (Legacy/Interim) does NOT decide the runtime: a hybrid setup
REM (Interim bootstrap + only a Legacy folder) runs a .NET 10 game through the
REM Legacy loader - a net48 DLL placed there is listed as enabled but silently
REM never loads (no error anywhere). If the game runs modern .NET and the
REM package has a net10 (Interim) build, we install THAT into every edition.
set "RUNTIME=unknown"
set "NEWESTLOG="
for /f "delims=" %%F in ('dir /b /o-d "%AppData%\SpaceEngineers\SpaceEngineers_*.log" 2^>nul') do (
    if not defined NEWESTLOG set "NEWESTLOG=%%F"
)
if not defined NEWESTLOG if exist "%AppData%\SpaceEngineers\SpaceEngineers.log" set "NEWESTLOG=SpaceEngineers.log"
if defined NEWESTLOG (
    findstr /r /c:"Environment.Version: \.NET [0-9]" "%AppData%\SpaceEngineers\%NEWESTLOG%" >nul 2>nul && set "RUNTIME=modern"
    findstr /c:"Environment.Version: .NET Framework" "%AppData%\SpaceEngineers\%NEWESTLOG%" >nul 2>nul && set "RUNTIME=framework"
)
if "%RUNTIME%"=="modern" (
    echo Game runtime detected: modern .NET ^(CoreCLR^) - net10 builds will be used.
)
if "%RUNTIME%"=="framework" (
    echo Game runtime detected: .NET Framework 4.x - net48 builds will be used.
)
if "%RUNTIME%"=="unknown" (
    echo Game runtime not detected ^(no game log yet^) - installing per edition folder.
)
echo.

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
    if exist "%PULSAR%\Modern" (
        echo NOTE: only Pulsar Modern was found - that is the Space Engineers 2
        echo loader. This SE1 plugin supports Legacy and Interim editions only.
    )
) else (
    echo Done. Start the game through Pulsar and test the plugin.
    echo To remove it later, run Uninstall-TestPlugin.bat.
    if exist "%PULSAR%\Modern" echo NOTE: Pulsar Modern ^(Space Engineers 2 loader^) was skipped on purpose - SE1 plugins do not go there.
)
echo.
pause
exit /b 0

REM ------------------------------------------------------------
:install_edition
set "EDITION=%~1"
set "PROFILE=%PULSAR%\%EDITION%\Profiles\Current.xml"

if not exist "%PROFILE%" if not exist "%PULSAR%\%EDITION%\Local" (
    echo [Pulsar %EDITION%] not installed here, skipping.
    exit /b 0
)

if not exist "%SCRIPT_DIR%Plugin\%EDITION%" (
    echo [Pulsar %EDITION%] this package has no %EDITION% build, skipping.
    exit /b 0
)

REM Which build flavor goes into this edition? On a modern .NET game the net10
REM (Interim) build is the one that actually loads, whatever the folder is called.
set "SRCED=%EDITION%"
if "%RUNTIME%"=="modern" if exist "%SCRIPT_DIR%Plugin\Interim" set "SRCED=Interim"
if "%RUNTIME%"=="framework" if exist "%SCRIPT_DIR%Plugin\Legacy" set "SRCED=Legacy"
if not "%SRCED%"=="%EDITION%" (
    echo [Pulsar %EDITION%] game runs a different runtime - using the %SRCED% build here.
)
if not exist "%SCRIPT_DIR%Plugin\%SRCED%" (
    if "%RUNTIME%"=="modern" (
        echo [Pulsar %EDITION%] WARNING: this package has no net10 build, but your game
        echo runs modern .NET - a net48-only install will NOT load. Ask for an updated package.
    )
    echo [Pulsar %EDITION%] this package has no %SRCED% build, skipping.
    exit /b 0
)

set "DEST=%PULSAR%\%EDITION%\Local"
if not exist "%DEST%" mkdir "%DEST%"

echo [Pulsar %EDITION%] copying plugin files...
copy /y "%SCRIPT_DIR%Plugin\%SRCED%\*" "%DEST%\" >nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: could not copy to "%DEST%".
    echo Make sure the game is CLOSED and try again.
    exit /b 2
)

REM Report each DLL's runtime family so a wrong-edition file is visible immediately
REM (a .NET Core DLL in Legacy - or a .NET Framework DLL in Interim - is exactly what
REM causes a runtime warning in Pulsar's plugin list and the plugin not loading).
for %%D in ("%SCRIPT_DIR%Plugin\%SRCED%\*.dll") do (
    findstr /m /c:".NETCoreApp" "%%~fD" >nul 2>nul && (
        echo [Pulsar %EDITION%] %%~nxD = .NET Core ^(CoreCLR^) build
    ) || (
        echo [Pulsar %EDITION%] %%~nxD = .NET Framework 4.8 ^(CLR^) build
    )
)

set "ENABLED_ANY=0"
for %%D in ("%SCRIPT_DIR%Plugin\%SRCED%\*.dll") do (
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
