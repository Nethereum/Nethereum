rem packing web3 and dependencies
echo on 

del /S compiledlibraries\netStandardUnityAOT\*.dll
SET releaseSuffix=
SET targetNet35=false

cd Nethereum.Hex
CALL :build
echo "1"
pause
cd ..

cd Nethereum.ABI
CALL :build
echo "2"
pause
cd ..

cd Nethereum.JsonRpc.Client*
CALL :build
echo "3"
pause
cd ..

cd Nethereum.RPC
CALL :build
echo "4"
pause
cd ..

cd Nethereum.Web3
CALL :build
echo "5"
pause
cd ..

cd Nethereum.JsonRpc.IpcClient
CALL :build
echo "6"
pause
cd ..

cd Nethereum.JsonRpc.WebSocket*
CALL :build
echo "7"
pause
cd ..

cd Nethereum.JsonRpc.RpcClient
CALL :build
echo "8"
pause
cd ..

cd Nethereum.KeyStore
CALL :build
echo "9"
pause
cd ..

cd Nethereum.Quorum
CALL :build
echo "10"
pause
cd ..

cd Nethereum.Geth
CALL :build
echo "11"
pause
cd ..

cd Nethereum.Contracts
CALL :build
echo "12"
pause
cd ..

cd Nethereum.RLP
CALL :build
echo "13"
pause
cd ..

cd Nethereum.Signer
CALL :build
echo "14"
pause
cd ..

cd Nethereum.Util
CALL :build
echo "15"
pause
cd ..

cd Nethereum.HdWallet*
CALL :build
echo "16"
pause
cd ..

cd Nethereum.Parity*
CALL :build
echo "17"
pause
cd ..

cd Nethereum.Accounts
CALL :build
echo "18"
pause
cd ..

cd Nethereum.Unity
CALL :build
echo "19"
pause
cd ..

cd Nethereum.RPC.Reactive
CALL :build
echo "20"
pause
cd ..

cd Nethereum.Besu
CALL :build
echo "21"
pause
cd ..

cd Nethereum.Signer.EIP712
CALL :build
echo "22"
pause
cd..

cd Nethereum.GnosisSafe
CALL :build
echo "23"
pause
cd ..

cd Nethereum.Siwe.Core
CALL :build
echo "24"
pause
cd ..

cd Nethereum.Siwe
CALL :build
echo "25"
pause
cd ..

cd Nethereum.BlockchainProcessing
CALL :build
echo "26"
pause
cd ..

cd Nethereum.Optimism
CALL :build
echo "27"
pause
cd ..

EXIT /B %ERRORLEVEL%

:build
rem dotnet clean /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
rem  dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
dotnet build  -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNetStandard=true /property:TargetNet35=false /property:TargetUnityAOT=true
xcopy bin\Release\netstandard2.0\*.dll "..\compiledlibraries\netStandardUnityAOT" /s /y
EXIT /B 0
