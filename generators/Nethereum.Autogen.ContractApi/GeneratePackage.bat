echo off
echo.Clean
dotnet clean -c release
echo.Publishing
cd ../Nethereum.Generator.Console
dotnet publish -c release ---framework net8.0
echo.Packing
cd ../Nethereum.Autogen.ContractApi
dotnet pack -c release
echo.Finished
