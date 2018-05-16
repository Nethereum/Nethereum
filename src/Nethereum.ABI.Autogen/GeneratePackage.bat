echo off
echo.Clean
dotnet clean -c release
echo.Publishing
dotnet publish -c release
echo.Packing
dotnet pack -c release --no-restore
echo.Finished
