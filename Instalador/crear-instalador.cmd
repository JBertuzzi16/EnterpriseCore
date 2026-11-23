@echo off
rem ==========================================================================
rem  Genera el instalador de GenteFit (Instalador\Salida\setup.exe).
rem  Requisitos en el equipo del desarrollador:
rem    - SDK de .NET 10 (viene con Visual Studio 2026)
rem    - Inno Setup 6 o 7 (https://jrsoftware.org/isdl.php)
rem  Uso: doble clic sobre este fichero.
rem ==========================================================================
setlocal
chcp 65001 >nul
cd /d "%~dp0.."

echo.
echo [1/3] Limpiando la publicacion anterior...
if exist "Instalador\publicacion" rmdir /s /q "Instalador\publicacion"

rem Hace falta el SDK de .NET 10 (el de Visual Studio 2022 no sirve).
where dotnet >nul 2>nul
if errorlevel 1 goto sinsdk
dotnet --list-sdks | findstr /b "10." >nul
if errorlevel 1 goto sinsdk

echo [2/3] Publicando GenteFit.web (Release, win-x64, autocontenida)...
dotnet publish "GenteFit.web\GenteFit.web.csproj" -c Release -r win-x64 --self-contained true -o "Instalador\publicacion" -nologo
if errorlevel 1 goto error

if not exist "Instalador\publicacion\GenteFit.web.exe" goto sinexe

rem La configuracion de desarrollo no se instala.
if exist "Instalador\publicacion\appsettings.Development.json" del /q "Instalador\publicacion\appsettings.Development.json"

echo [3/3] Compilando el instalador con Inno Setup...
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if exist "%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
if not defined ISCC (
    echo.
    echo No se encuentra Inno Setup ^(version 6 o 7^). Instalalo desde https://jrsoftware.org/isdl.php y vuelve a ejecutar este fichero.
    goto error
)

"%ISCC%" /Q "Instalador\GenteFit.iss"
if errorlevel 1 goto error

echo.
echo Instalador creado: %CD%\Instalador\Salida\setup.exe
echo.
pause
exit /b 0

:sinsdk
echo.
echo No se encuentra el SDK de .NET 10. Instala Visual Studio 2026 o el SDK de .NET 10
echo desde https://dotnet.microsoft.com/download y vuelve a ejecutar este fichero.
echo SDK instalados ahora:
dotnet --list-sdks 2>nul
goto error

:sinexe
echo.
echo La publicacion ha terminado sin Instalador\publicacion\GenteFit.web.exe.
echo Si un antivirus lo ha bloqueado, permite la carpeta del proyecto y vuelve a intentarlo.
goto error

:error
echo.
echo *** No se ha podido crear el instalador. Revisa los mensajes de arriba. ***
echo.
pause
exit /b 1
