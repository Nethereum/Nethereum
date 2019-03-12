REM use -k ~~your API key here~~ to push symbols
cd packages
FOR %%i IN (*.nupkg) DO dotnet nuget push %%i -s https://api.nuget.org/v3/index.json
cd..
