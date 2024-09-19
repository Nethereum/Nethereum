rem packing web3 and dependencies
del /S compiledlibraries\netStandard\*.dll
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
SET projectName=Nethereum.Web3.csproj
CALL :build
SET projectName=
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

cd Nethereum.KeyStore
CALL :build
cd ..

cd Nethereum.Geth
CALL :build
cd ..

cd Nethereum.Quorum
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

cd Nethereum.Siwe
CALL :build
cd ..

cd Nethereum.BlockchainProcessing
CALL :build
cd..

cd Nethereum.Optimism
CALL :build
cd ..

cd Nethereum.EVM
CALL :build
cd ..

cd Nethereum.UI
CALL :build
cd ..


cd Nethereum.Merkle
CALL :build
cd ..

cd Nethereum.Merkle.Patricia
CALL :build
cd ..

cd Nethereum.Metamask
CALL :build
cd ..

cd Nethereum.Model
CALL :build
cd ..

cd Nethereum.Mud
CALL :build
cd ..

cd Nethereum.Mud.Contracts
CALL :build
cd ..

cd Nethereum.Util.RestApi
CALL :build
cd ..

EXIT /B %ERRORLEVEL%

:build
rem dotnet clean /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=false
rem  dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=false
dotnet build %projectName% -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=false
IF %ERRORLEVEL% EQU 0 (
    xcopy bin\Release\netstandard2.0\*.dll "..\compiledlibraries\netStandard" /s /y
    EXIT /B 0
) ELSE (
    EXIT %ERRORLEVEL%
)