rem packing web3 and dependencies
del /S *.*.nupkg
SET releaseSuffix=rc6
SET targetNet35=false
cd Nethereum.Hex
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.ABI
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.JsonRpc.Client
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.RPC
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Web3
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.StandardToken*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.JsonRpc.IpcClient*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.JsonRpc.RpcClient*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.KeyStore*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.ENS*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Quorum*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Geth*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Contracts*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.RLP*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Signer*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Util*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
cd Nethereum.Uport*
dotnet restore /property:ReleaseSuffix=%releaseSuffix%
dotnet pack /property:TargetNet35=%targetNet35%
cd ..
setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0\packages
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages /s /y