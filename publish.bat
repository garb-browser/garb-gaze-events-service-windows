@echo off
echo ============================================
echo GARB Eye Tracking Service - Build & Package
echo ============================================
echo.

REM Check if MSBuild is available
where msbuild >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo MSBuild not found in PATH.
    echo Please run this from Developer Command Prompt for Visual Studio
    echo Or add MSBuild to your PATH.
    pause
    exit /b 1
)

echo [1/4] Restoring NuGet packages...
nuget restore WindowsEyeServer.sln
if %ERRORLEVEL% NEQ 0 (
    echo Failed to restore packages. Trying with dotnet...
    dotnet restore WindowsEyeServer.sln
)

echo.
echo [2/4] Building Release configuration...
msbuild WindowsEyeServer.sln /p:Configuration=Release /p:Platform="Any CPU" /v:minimal
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo [3/4] Creating release package...
if exist "publish" rmdir /s /q "publish"
mkdir "publish"

REM Copy build output
xcopy /E /I /Y "bin\Release\*" "publish\"

REM Ensure native Tobii DLLs are included (x64)
if exist "packages\Tobii.Interaction.0.7.1\build\x64\Tobii.EyeX.Client.dll" (
    copy /Y "packages\Tobii.Interaction.0.7.1\build\x64\Tobii.EyeX.Client.dll" "publish\"
)

echo.
echo [4/4] Done!
echo.
echo ============================================
echo Release package created in: publish\
echo.
echo To distribute:
echo   1. ZIP the 'publish' folder
echo   2. Users extract and run GARB-Eye-Tracker.exe
echo.
echo Note: Users need .NET Framework 4.8 installed
echo       (pre-installed on Windows 10/11)
echo ============================================
echo.
pause
