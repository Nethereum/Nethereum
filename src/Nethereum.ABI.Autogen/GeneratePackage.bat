echo off
echo.Publishing
dotnet publish -c release
echo.Packing
dotnet pack -c release
echo.Finished
