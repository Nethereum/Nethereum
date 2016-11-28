rem packing web3 and dependencies
del /S *.*.nupkg
cd Nethereum.Hex
dotnet pack
cd ..
cd Nethereum.ABI
dotnet pack
cd ..
cd Nethereum.JsonRpc.Client
dotnet pack
cd ..
cd Nethereum.RPC
dotnet pack
cd ..
cd Nethereum.Web3
dotnet pack
cd ..
cd Nethereum.StandardToken*
dotnet pack
cd ..
cd Nethereum.JsonRpc.IpcClient*
dotnet pack
cd ..
cd Nethereum.KeyStore*
dotnet pack
cd ..
setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0\packages
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages /s /y