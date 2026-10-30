@echo off
setlocal

REM ===========================================================================
REM  Runs "classify" from a Release build made a moment ago.
REM
REM  classify proposes effects with the rules compiled into the exe, and dotnet
REM  run and dotnet test only ever refresh Debug. On 2026-09-24 a classify from
REM  a five-day-old Release build rewrote every unreviewed proposal with rules
REM  that had since been fixed (see tagger.cmd). So, unlike tagger.cmd, a failed
REM  build or a missing dotnet is FATAL here: the old exe is never used.
REM
REM  Every argument is passed through:
REM    classify.cmd --dry-run
REM    classify.cmd --overwrite
REM ===========================================================================

set "EXE=%~dp0ScatoloneDownloader\bin\Release\net10.0\ScatoloneDownloader.exe"

where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet
echo Building Release...
dotnet build "%~dp0ScatoloneDownloader" -c Release -v q --nologo >"%TEMP%\classify-build.log" 2>&1
if errorlevel 1 goto :buildfailed

"%EXE%" classify %*
exit /b %errorlevel%

:nodotnet
echo [!] dotnet is not on PATH - classify will NOT run: it must not use a stale build.
exit /b 1

:buildfailed
echo [!] Build FAILED - classify will NOT run from a stale build.
echo     Its rules may be old and would rewrite the unreviewed proposals.
echo     Log: %TEMP%\classify-build.log
exit /b 1
