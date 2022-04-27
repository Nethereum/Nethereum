rem packing web3 and dependencies
del /S compiledlibraries\net472dllsAOT\*.dll
SET releaseSuffix=
SET targetNet35=false

cd Nethereum.Hex
CALL :build
cd ..

cd Nethereum.ABI
CALL :build
cd ..

cd Nethereum.JsonRpc.Client
CALL :build
cd ..

cd Nethereum.RPC
CALL :build
cd ..

cd Nethereum.Web3
CALL :build
cd ..

cd Nethereum.StandardToken*
CALL :build
cd ..

cd Nethereum.JsonRpc.IpcClient*
CALL :build
cd ..

cd Nethereum.JsonRpc.WebSocket*
CALL :build
cd ..

cd Nethereum.JsonRpc.RpcClient*
CALL :build
cd ..

cd Nethereum.KeyStore*
CALL :build
cd ..

cd Nethereum.Quorum*
CALL :build
cd ..

cd Nethereum.Geth*
CALL :build
cd ..

cd Nethereum.Contracts*
CALL :build
cd ..

cd Nethereum.RLP*
CALL :build
cd ..

cd Nethereum.Signer
CALL :build
cd ..

cd Nethereum.Util
CALL :build
cd ..

cd Nethereum.HdWallet*
CALL :build
cd ..

cd Nethereum.Parity*
CALL :build
cd ..

cd Nethereum.Accounts*
CALL :build
cd ..

cd Nethereum.Unity*
CALL :build
cd ..

cd Nethereum.RPC.Reactive
CALL :build
cd ..

cd Nethereum.Besu
CALL :build
cd ..

cd Nethereum.Signer.EIP712
CALL :build
cd..

cd Nethereum.GnosisSafe
CALL :build
cd ..

cd Nethereum.Siwe.Core
CALL :build
cd ..

cd Nethereum.BlockchainProcessing
CALL :build
cd..

cd Nethereum.Optimism
CALL :build
cd ..

EXIT /B %ERRORLEVEL%

:build
rem dotnet clean /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
rem  dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
dotnet build  -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
xcopy bin\Release\net472\*.dll "..\compiledlibraries\net472dllsAOT" /s /y
EXIT /B 0