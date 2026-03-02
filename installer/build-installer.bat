@echo off
echo ================================================
echo GARB Eye Tracking Service - Full Build + Installer
echo ================================================
echo.

set GARB_ROOT=C:\Users\chris\OneDrive\Documents\Garb\garb-gaze-events-service-windows
set INSTALLER_DIR=%~dp0

REM Step 1: Build the project
echo [1/3] Building Release configuration...
cd /d "%GARB_ROOT%"

REM Try MSBuild from Visual Studio
set MSBUILD=
for /f "tokens=*" %%i in ('where msbuild 2^>nul') do set MSBUILD=%%i
if "%MSBUILD%"=="" (
    REM Try VS2022 default location
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
    )
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
    )
)

if "%MSBUILD%"=="" (
    echo ERROR: MSBuild not found. Install Visual Studio Build Tools or run from Developer Command Prompt.
    pause
    exit /b 1
)

REM Restore NuGet packages
echo Restoring NuGet packages...
nuget restore WindowsEyeServer.sln 2>nul
if %ERRORLEVEL% NEQ 0 (
    dotnet restore WindowsEyeServer.sln 2>nul
)

"%MSBUILD%" WindowsEyeServer.sln /p:Configuration=Release /p:Platform="Any CPU" /v:minimal
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

REM Step 2: Create publish directory
echo.
echo [2/3] Packaging release files...
if exist "%INSTALLER_DIR%publish" rmdir /s /q "%INSTALLER_DIR%publish"
mkdir "%INSTALLER_DIR%publish"

xcopy /E /I /Y "%GARB_ROOT%\bin\Release\*" "%INSTALLER_DIR%publish\"

REM Ensure Tobii native DLL is included
if exist "%GARB_ROOT%\packages\Tobii.Interaction.0.7.1\build\x64\Tobii.EyeX.Client.dll" (
    copy /Y "%GARB_ROOT%\packages\Tobii.Interaction.0.7.1\build\x64\Tobii.EyeX.Client.dll" "%INSTALLER_DIR%publish\"
    echo Copied Tobii.EyeX.Client.dll
)

REM Step 3: Compile installer
echo.
echo [3/3] Compiling Inno Setup installer...

set ISCC=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files\Inno Setup 6\ISCC.exe

if "%ISCC%"=="" (
    echo WARNING: Inno Setup 6 not found.
    echo Install from: https://jrsoftware.org/isdl.php
    echo.
    echo Publish directory is ready at: %INSTALLER_DIR%publish\
    echo You can compile the installer manually with ISCC.exe garb-installer.iss
    pause
    exit /b 0
)

"%ISCC%" "%INSTALLER_DIR%garb-installer.iss"
if %ERRORLEVEL% NEQ 0 (
    echo Installer compilation failed!
    pause
    exit /b 1
)

echo.
echo ================================================
echo SUCCESS! Installer created at:
echo   %INSTALLER_DIR%output\GARB-Eye-Tracking-Service-Setup.exe
echo ================================================
pause
