echo off
echo.Clean
dotnet clean -c release
echo.Publishing
cd ../Nethereum.Generator.Console
dotnet publish -c release
echo.Packing
cd ../Nethereum.ABI.Autogen
dotnet pack -c release
echo.Finished
