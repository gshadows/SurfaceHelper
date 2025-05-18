@echo off

set "ObservatoryPath=%APPDATA%\..\Local\Programs\Elite Observatory"
set PluginName=SurfaceHelper

call uninstall 2>nul
copy /B "build\x64-Debug\%PluginName%.dll" "%ObservatoryPath%\plugins\"
