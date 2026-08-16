@echo off
setlocal enabledelayedexpansion
title SE test plugin diagnostics

REM ============================================================
REM  Generic tester diagnostics for any Pulsar SE1 test plugin
REM  package. Collects the environment info needed to diagnose
REM  plugin loading problems ("runtime!" / "Host!" / "Error!"
REM  badge, plugin not loaded).
REM
REM  Usage: Diagnostics.bat [pulsar-folder]
REM  The PLUGIN NAME is auto-detected from the package layout
REM  (Plugin\Legacy\*.dll or Plugin\Interim\*.dll next to this
REM  script) - no per-plugin edits needed. Output:
REM  Diagnostics-report.txt next to this script AND on the
REM  Desktop.
REM
REM  Why this exists: Pulsar writes NOTHING to its log when it
REM  skips a plugin for a runtime/environment mismatch - the
REM  plugin just shows a badge in the list and never loads.
REM  This script reconstructs that state instead: which edition
REM  the tester runs, which runtime build of the plugin got
REM  installed there, whether it is enabled, the Pulsar log
REM  tail, the newest game log's plugin lines, and the
REM  installed .NET runtimes.
REM ============================================================

set "SCRIPT_DIR=%~dp0"
set "OUT=%SCRIPT_DIR%Diagnostics-report.txt"

set "PULSAR=%~1"
if not defined PULSAR set "PULSAR=%PULSAR_DIR%"
if not defined PULSAR set "PULSAR=%AppData%\Pulsar"

REM --- auto-detect the plugin name from the package layout ---
set "NAME="
if exist "%SCRIPT_DIR%Plugin\Legacy\*.dll" for %%D in ("%SCRIPT_DIR%Plugin\Legacy\*.dll") do if not defined NAME set "NAME=%%~nD"
if not defined NAME if exist "%SCRIPT_DIR%Plugin\Interim\*.dll" for %%D in ("%SCRIPT_DIR%Plugin\Interim\*.dll") do if not defined NAME set "NAME=%%~nD"
if not defined NAME set /p "NAME=Plugin name (DLL file name without .dll): "
if not defined NAME set "NAME=UnknownPlugin"

> "%OUT%" echo %NAME% - tester diagnostics
>>"%OUT%" echo Date: %date% %time%
>>"%OUT%" echo OS: %OS% / %PROCESSOR_ARCHITECTURE%
>>"%OUT%" echo Pulsar folder: %PULSAR%
>>"%OUT%" echo.

set /p WHICH="Which Pulsar do you launch the game with? (Legacy / Interim / Modern / not sure): "
>>"%OUT%" echo Tester launches Pulsar edition: %WHICH%
>>"%OUT%" echo.

if not exist "%PULSAR%" (
    >>"%OUT%" echo Pulsar folder NOT FOUND: "%PULSAR%"
    >>"%OUT%" echo Install Pulsar first, or pass its folder as argument:
    >>"%OUT%" echo     Diagnostics.bat "C:\path\to\Pulsar"
    goto :runtimes
)

for %%E in (Legacy Interim Modern) do call :edition "%%E"

:runtimes
>>"%OUT%" echo.
>>"%OUT%" echo === .NET runtimes installed (dotnet --list-runtimes) ===
>>"%OUT%" echo (Pulsar Legacy needs none; Pulsar Interim needs the .NET 10 Desktop Runtime)
where dotnet >nul 2>nul
if errorlevel 1 (
    >>"%OUT%" echo dotnet CLI not found on PATH - the .NET 10 Desktop Runtime may be missing.
    >>"%OUT%" echo Install it from https://dotnet.microsoft.com/download/dotnet/10.0
) else (
    dotnet --list-runtimes >> "%OUT%" 2>&1
)

>>"%OUT%" echo.
>>"%OUT%" echo === Game log (plugin only runs when the game loads it) ===
powershell -NoProfile -Command "$log = Get-ChildItem -LiteralPath ($env:APPDATA+'\SpaceEngineers') -Filter 'SpaceEngineers*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1; if ($log) { 'newest game log: ' + $log.Name; $m = Select-String -LiteralPath $log.FullName -SimpleMatch ('['+'%NAME%'+']') | Select-Object -Last 10; if ($m) { $m | ForEach-Object { $_.Line } } else { 'none - the plugin did not run in the last game session' } } else { 'no game log found - has the game been started through Pulsar yet?' }" >> "%OUT%" 2>nul

copy /y "%OUT%" "%USERPROFILE%\Desktop\Diagnostics-report.txt" >nul 2>nul
echo.
echo Report written to:
echo   %OUT%
if exist "%USERPROFILE%\Desktop\Diagnostics-report.txt" echo   %USERPROFILE%\Desktop\Diagnostics-report.txt
echo Send the file back together with your tester feedback.
echo.
pause
exit /b 0

REM ------------------------------------------------------------
:edition
set "EDITION=%~1"
set "DIR=%PULSAR%\%EDITION%"
>>"%OUT%" echo.
>>"%OUT%" echo === Pulsar %EDITION% ===
if not exist "%DIR%" (
    >>"%OUT%" echo not present
    goto :eof
)
if "%EDITION%"=="Modern" >>"%OUT%" echo NOTE: Modern is the Space Engineers 2 loader - SE1 plugins do NOT belong here.

set "VER="
if exist "%DIR%\info.log" for /f "delims=" %%L in ('findstr /c:"Starting Pulsar v" "%DIR%\info.log"') do if not defined VER set "VER=%%L"
if defined VER (>>"%OUT%" echo !VER!) else (>>"%OUT%" echo version unknown - no info.log found)

set "DLL=%DIR%\Local\%NAME%.dll"
if not exist "%DLL%" (
    >>"%OUT%" echo %NAME%.dll NOT installed in %EDITION%\Local
    goto :eof
)
set "TFM=unknown runtime string (file corrupt?)"
findstr /m /c:".NETCoreApp" "%DLL%" >nul 2>nul && set "TFM=.NET Core build (CoreCLR) - belongs in Interim only"
findstr /m /c:".NETFramework,Version=v4.8" "%DLL%" >nul 2>nul && set "TFM=.NET Framework 4.8 build (CLR) - belongs in Legacy only"
>>"%OUT%" echo installed DLL: %TFM%
findstr /c:"%NAME%.dll" "%DIR%\Profiles\Current.xml" >nul 2>nul && (>>"%OUT%" echo enabled in profile: yes) || (>>"%OUT%" echo enabled in profile: no)

if exist "%DIR%\info.log" (
    >>"%OUT%" echo last relevant lines from info.log:
    powershell -NoProfile -Command "Get-Content -Literal '%DIR%\info.log' -ErrorAction SilentlyContinue | Select-String -Pattern '%NAME%','Failed','Error','Exception','Warn','Harmony' | Select-Object -Last 15 | ForEach-Object { $_.Line }" >> "%OUT%" 2>nul
    >>"%OUT%" echo.
)
goto :eof
