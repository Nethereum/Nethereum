rem packing web3 and dependencies
del /S *.*.nupkg
SET releaseSuffix=rc4
SET targetNet35=false

cd Nethereum.Hex
CALL :restorepack
cd ..

cd Nethereum.ABI
CALL :restorepack
cd ..

cd Nethereum.JsonRpc.Client
CALL :restorepack
cd ..

cd Nethereum.RPC
CALL :restorepack
cd ..

cd Nethereum.Web3
CALL :restorepack
cd ..

cd Nethereum.StandardToken*
CALL :restorepack
cd ..

cd Nethereum.JsonRpc.IpcClient*
CALL :restorepack
cd ..

cd Nethereum.JsonRpc.RpcClient*
CALL :restorepack
cd ..

cd Nethereum.JsonRpc.WebSocketClient*
CALL :restorepack
cd ..

cd Nethereum.KeyStore*
CALL :restorepack
cd ..

cd Nethereum.ENS*
CALL :restorepack
cd ..

cd Nethereum.Quorum*
CALL :restorepack
cd ..

cd Nethereum.Geth*
CALL :restorepack
cd ..

cd Nethereum.Contracts*
CALL :restorepack
cd ..

cd Nethereum.RLP*
CALL :restorepack
cd ..

cd Nethereum.Signer
CALL :restorepack
cd ..

cd Nethereum.Signer.AzureKeyVault
CALL :restorepack
cd ..

cd Nethereum.Signer.Ledger
CALL :restorepack
cd ..

cd Nethereum.Signer.Trezor
CALL :restorepack
cd ..

cd Nethereum.Util*
CALL :restorepack
cd ..

cd Nethereum.Uport*
CALL :restorepack
cd ..

cd Nethereum.HdWallet*
CALL :restorepack
cd ..

cd Nethereum.Parity*
CALL :restorepack
cd ..

cd Nethereum.Accounts*
CALL :restorepack
cd ..

setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0\packages
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages /s /y

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet35=%targetNet35%
dotnet build -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
dotnet pack -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
EXIT /B 0