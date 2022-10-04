rem packing web3 and dependencies
rem echo on 

del /S compiledlibraries\netStandardUnityAOT\*.dll
SET releaseSuffix=
SET targetNet35=false

cd Nethereum.Hex
CALL :build
rem echo "1"
rem pause
cd ..

cd Nethereum.ABI
CALL :build
rem echo "2"
rem pause
cd ..

cd Nethereum.JsonRpc.Client*
CALL :build
rem echo "3"
rem pause
cd ..

cd Nethereum.RPC
CALL :build
rem echo "4"
rem pause
cd ..

cd Nethereum.Web3
CALL :build
rem echo "5"
rem pause
cd ..

cd Nethereum.JsonRpc.IpcClient
CALL :build
rem echo "6"
rem pause
cd ..

cd Nethereum.JsonRpc.WebSocket*
CALL :build
rem echo "7"
rem pause
cd ..

cd Nethereum.JsonRpc.RpcClient
CALL :build
rem echo "8"
rem pause
cd ..

cd Nethereum.KeyStore
CALL :build
rem echo "9"
rem pause
cd ..

cd Nethereum.Quorum
CALL :build
rem echo "10"
rem pause
cd ..

cd Nethereum.Geth
CALL :build
rem echo "11"
rem pause
cd ..

cd Nethereum.Contracts
CALL :build
rem echo "12"
rem pause
cd ..

cd Nethereum.RLP
CALL :build
rem echo "13"
rem pause
cd ..

cd Nethereum.Signer
CALL :build
rem echo "14"
rem pause
cd ..

cd Nethereum.Util
CALL :build
rem echo "15"
rem pause
cd ..

cd Nethereum.HdWallet*
CALL :build
rem echo "16"
rem pause
cd ..

cd Nethereum.Parity*
CALL :build
rem echo "17"
rem pause
cd ..

cd Nethereum.Accounts
CALL :build
rem echo "18"
rem pause
cd ..

cd Nethereum.Unity
CALL :build
rem echo "19"
rem pause
cd ..

cd Nethereum.Unity.Metamask
CALL :build
cd ..

cd Nethereum.RPC.Reactive
CALL :build
rem echo "20"
rem pause
cd ..

cd Nethereum.Besu
CALL :build
rem echo "21"
rem pause
cd ..

cd Nethereum.Signer.EIP712
CALL :build
rem echo "22"
rem pause
cd..

cd Nethereum.GnosisSafe
CALL :build
rem echo "23"
rem pause
cd ..

cd Nethereum.Siwe.Core
CALL :build
rem echo "24"
rem pause
cd ..

cd Nethereum.Siwe
CALL :build
rem echo "25"
rem pause
cd ..

cd Nethereum.BlockchainProcessing
CALL :build
rem echo "26"
rem pause
cd ..

cd Nethereum.Optimism
CALL :build
rem echo "27"
rem pause
cd ..

EXIT /B %ERRORLEVEL%

:build
rem dotnet clean /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
rem  dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
dotnet build  -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
xcopy bin\Release\netstandard2.0\*.dll "..\compiledlibraries\netStandardUnityAOT" /s /y
EXIT /B 0
