@echo off
chcp 65001 >nul
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Не найден компилятор .NET Framework: %CSC%
  pause
  exit /b 1
)
taskkill /im GlassIntro.exe /f >nul 2>&1
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /out:GlassIntro.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll GlassIntro.cs
if errorlevel 1 (
  echo.
  echo Сборка не удалась. Сделай снимок этого окна и пришли его в чат.
  pause
  exit /b 1
)
echo Готово: значок GlassIntro появился в трее, рядом с часами.
start "" "%~dp0GlassIntro.exe"
timeout /t 4 >nul
