rem packing web3 and dependencies
del /S compiledlibraries\net461dlls\*.dll
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

cd Nethereum.Pantheon
CALL :build
cd ..


EXIT /B %ERRORLEVEL%

:build
dotnet build -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet461=true /property:TargetNet35=false
xcopy bin\Release\net461\*.dll "..\compiledlibraries\net461dlls" /s /y
EXIT /B 0