@echo off
setlocal
cd /d "%~dp0"
dotnet publish CashButton.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo ERROR: No se pudo compilar. Verifica que este instalado el SDK .NET 8.
  pause
  exit /b 1
)
echo.
echo Aplicacion creada en:
echo %CD%\bin\Release\net8.0-windows\win-x64\publish\CashButton.exe
pause
