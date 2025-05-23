@echo off

set "ObservatoryPath=%APPDATA%\..\Local\Programs\Elite Observatory"
set PluginName=SurfaceHelper


call :BUILD x64 Debug
rem call :BUILD x86 Debug
call :BUILD x64 Release
rem call :BUILD x86 Release

goto :EOF

:BUILD
dotnet build -a %1 -c %2 --nologo -o .\build\%1-%2\ -p:ObservatoryPath="%ObservatoryPath%" -p:PluginName=%PluginName%
exit /B
