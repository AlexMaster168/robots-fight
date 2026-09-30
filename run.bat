@echo off
setlocal
cd /d "%~dp0"

if not exist "Builds\Windows\RobotsFight.exe" (
    echo No build yet - building first...
    call "%~dp0build.bat" nopause
    if errorlevel 1 (
        pause
        exit /b 1
    )
)

rem All arguments go to the game, e.g.: run.bat -screen-fullscreen 1
start "" "Builds\Windows\RobotsFight.exe" %*
