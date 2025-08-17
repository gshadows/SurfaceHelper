@echo off

set PluginName=SurfaceHelper
set VERSION=0.0.3

set ZIPFILE=..\..\releases\%PluginName%.%VERSION%.eop

set SZ="C:\Program Files\7-Zip\7z.exe"

if not exist build\x64-Release (
	echo "Release not found!"
	exit 1
)
pushd build\x64-Release

del /Q %ZIPFILE% 2>nul
%SZ% a -tzip %ZIPFILE% %PluginName%.dll

popd
