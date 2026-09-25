@echo off
setlocal

REM ===========================================================================
REM  Starts the cube tagger so that BOTH this PC and the phone can reach it.
REM
REM  The --host flags are the whole point: http.sys routes on the request's
REM  Host header, and "tailscale serve" forwards to localhost while preserving
REM  the original Host. Without them the phone gets 400 Invalid Hostname.
REM
REM  Edit the four values below if the library moves or the tailnet changes.
REM  The three matching "netsh http add urlacl" reservations have to change
REM  with the host names -- see the startup error, it prints them.
REM ===========================================================================

set "SOURCE=E:\Working\Repos\ScatoloneQuintet\Source"
set "HOST_SHORT=cala"
set "HOST_FULL=cala.tail6de9de.ts.net"
set "TAILNET_PORT=8080"

set "EXE=%~dp0ScatoloneDownloader\bin\Release\net10.0\ScatoloneDownloader.exe"
set "TS=C:\Program Files\Tailscale\tailscale.exe"

REM Build first. Everything this exe does -- the tagger here, and a classify
REM run from it later -- uses the rules compiled into it, and dotnet run and
REM dotnet test only ever refresh Debug. On 2026-09-24 a classify from a
REM five-day-old Release build rewrote every unreviewed proposal with rules
REM that had since been fixed, and three days of review were shown them.
REM A failed build is not fatal -- the previous exe still tags -- but it is
REM said loudly, because that exe is exactly the stale one.
where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet
echo Building Release...
dotnet build "%~dp0ScatoloneDownloader" -c Release -v q --nologo >"%TEMP%\tagger-build.log" 2>&1
if errorlevel 1 goto :buildfailed
goto :checks

:nodotnet
echo [!] dotnet is not on PATH - starting the existing build, which may be stale.
goto :checks

:buildfailed
echo [!] Build FAILED - starting the previous build. Its rules may be stale:
echo     do not run classify from it until this is fixed. Log: %TEMP%\tagger-build.log

:checks
if not exist "%EXE%" goto :nobuild
if not exist "%SOURCE%" goto :nosource
if not exist "%TS%" goto :run

REM Non-fatal: the tagger is still perfectly usable from this PC without it.
"%TS%" serve status 2>nul | findstr /C:":8765" >nul
if errorlevel 1 echo [!] tailscale serve is not forwarding to 8765 - the phone URL will not answer.

:run
echo.
echo    PC        http://localhost:8765/
echo    Phone     http://%HOST_FULL%:%TAILNET_PORT%/
echo.
echo    Switching device? Reload the page. Each browser keeps its own copy of
echo    the cards, so a stale tab can overwrite a card you just tagged elsewhere.
echo.

"%EXE%" tag "%SOURCE%" --host "%HOST_SHORT%" --host "%HOST_FULL%"
goto :eof

:nobuild
echo Executable not found:
echo   %EXE%
echo Build it first:  dotnet build -c Release
echo.
pause
goto :eof

:nosource
echo Master library not found:
echo   %SOURCE%
echo Fix SOURCE at the top of this file.
echo.
pause
goto :eof
