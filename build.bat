@echo off
setlocal
cd /d "%~dp0"

rem Unity path: UNITY_PATH env var, or the version from ProjectSettings\ProjectVersion.txt via Unity Hub
if not defined UNITY_PATH (
    for /f "tokens=2" %%V in ('findstr /b "m_EditorVersion:" "ProjectSettings\ProjectVersion.txt"') do set "UNITY_VER=%%V"
)
if not defined UNITY_PATH set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\%UNITY_VER%\Editor\Unity.exe"

if not exist "%UNITY_PATH%" (
    echo [!] Unity not found: "%UNITY_PATH%"
    echo     Install the Unity version from ProjectSettings\ProjectVersion.txt via Unity Hub, or set UNITY_PATH.
    goto :fail
)

if not exist "Logs" mkdir "Logs"
echo Building Robots Fight with "%UNITY_PATH%" ...
echo (close the project in the Unity Editor first, otherwise the build will not start)
"%UNITY_PATH%" -batchmode -quit -projectPath "%~dp0." -executeMethod RobotsFight.EditorTools.RobotsFightTools.BuildWindows -logFile "%~dp0Logs\build.log"
if errorlevel 1 (
    echo [!] Build FAILED. Compile errors:
    findstr /c:"error CS" "Logs\build.log"
    echo Full log: Logs\build.log
    goto :fail
)
if not exist "Builds\Windows\RobotsFight.exe" (
    echo [!] Unity finished but the exe is missing. See Logs\build.log
    goto :fail
)

echo.
echo Done: Builds\Windows\RobotsFight.exe
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
if /i not "%~1"=="nopause" pause
exit /b 1
