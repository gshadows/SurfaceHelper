@echo off

set "ObservatoryPath=%APPDATA%\..\Local\Programs\Elite Observatory"
set PluginName=SurfaceHelper

del /Q "%ObservatoryPath%\plugins\%PluginName%*.dll"
