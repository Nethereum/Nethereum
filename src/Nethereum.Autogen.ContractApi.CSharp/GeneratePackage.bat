echo off
echo.Clean
dotnet clean -c release
echo.Publishing
cd ../Nethereum.Generator.Console
dotnet publish -c release
echo.Packing
cd ../Nethereum.Autogen.ContractApi.CSharp
dotnet pack -c release
echo.Finished
