cd packages
FOR %%i IN (*.nupkg) DO dotnet nuget push %%i -s https://www.nuget.org/api/v2/package
cd..
