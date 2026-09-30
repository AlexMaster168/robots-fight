@echo off
setlocal
cd /d "%~dp0"

rem Captures the menu and all 5 modes (bots play by themselves) into docs\screenshots. Takes ~2 minutes.
if not exist "Builds\Windows\RobotsFight.exe" (
    call "%~dp0build.bat" nopause
    if errorlevel 1 exit /b 1
)
echo Taking screenshots, do not touch the game window for ~2 minutes...
start "" /wait "Builds\Windows\RobotsFight.exe" -shots -shotsDir "%~dp0docs\screenshots" -screen-width 1600 -screen-height 900 -screen-fullscreen 0
echo Done: docs\screenshots
