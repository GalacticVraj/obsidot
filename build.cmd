@echo off
rem Builds Obsidot with the C# compiler that ships inside Windows (.NET Framework 4.x). No SDK needed.
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:"%~dp0Obsidot.exe" /win32manifest:"%~dp0src\app.manifest" /win32icon:"%~dp0src\obsidot.ico" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0src\*.cs"
