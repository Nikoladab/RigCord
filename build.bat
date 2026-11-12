@echo off
rem Builds RigCord.exe with the .NET Framework compiler that ships with Windows 10/11.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist src\app.ico powershell -NoProfile -ExecutionPolicy Bypass -File src\make-icon.ps1
if not exist assets\logo.png powershell -NoProfile -ExecutionPolicy Bypass -File src\make-icon.ps1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:src\app.ico /resource:assets\logo.png,logo.png ^
  /out:RigCord.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\RigCord.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo Done: RigCord.exe
