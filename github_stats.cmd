@echo off

mkdir releases 1>nul 2>nul
pushd releases

curl https://api.github.com/repos/gshadows/SurfaceHelper/releases -o releases.json

type releases.json | findstr "tag_name download_count" > releases-stat.txt

popd
