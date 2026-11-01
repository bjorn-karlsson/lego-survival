@echo off
rem BRICKBLADE: build the single-file Windows game, out\win\Brickblade.exe
rem Needs the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
rem The same command works from Linux or macOS (dotnet publish ... -r win-x64).
cd /d "%~dp0"
dotnet publish Brickblade.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o out\win
if errorlevel 1 (
  echo.
  echo The build failed. Is the .NET 8 SDK installed?  dotnet --version
  pause
  exit /b 1
)
echo.
echo Done: %~dp0out\win\Brickblade.exe
pause
