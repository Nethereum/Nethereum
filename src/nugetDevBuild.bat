rem packing web3 and dependencies

for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"

set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
set "fullstamp=%YYYY%%MM%%DD%%HH%%Min%%Sec%"


del /S *.*.nupkg
del /S *.*.snupkg
SET releaseSuffix=devbuildsecurityrisk%fullstamp%
SET targetNet35=false
SET projectName=


cd Nethereum.Web3
SET projectName=Nethereum.Web3.csproj
CALL :restorepack
SET projectName=Nethereum.Web3Lite.csproj
CALL :restorepack
SET projectName=
cd ..

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

cd Nethereum.Signer.AWSKeyManagement
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

cd Nethereum.HDWallet
CALL :restorepack
cd ..

cd Nethereum.Parity*
CALL :restorepack
cd ..

cd Nethereum.Accounts*
CALL :restorepack
cd ..

cd Nethereum.Parity.Reactive
CALL :restorepack
cd..

cd Nethereum.RPC.Reactive
CALL :restorepack
cd..

cd Nethereum.Model
CALL :restorepack
cd..

cd Nethereum.StandardNonFungibleTokenERC721
CALL :restorepack
cd..

cd Nethereum.Besu
CALL :restorepack
cd..

cd Nethereum.RSK
CALL :restorepack
cd..

cd Nethereum.BlockchainProcessing
CALL :restorepack
cd..

cd Nethereum.Signer.EIP712
CALL :restorepack
cd..

cd Nethereum.GnosisSafe
CALL :restorepack
cd ..

cd Nethereum.Siwe.Core
CALL :restorepack
cd ..

cd Nethereum.Siwe
CALL :restorepack
cd ..

cd Nethereum.Optimism
CALL :restorepack
cd ..

cd Nethereum.Metamask
CALL :restorepack
cd ..

cd Nethereum.Metamask.Blazor
CALL :restorepack
cd ..

cd Nethereum.UI
CALL :restorepack
cd ..

cd Nethereum.EVM
CALL :restorepack
cd ..

cd Nethereum.Merkle
CALL :restorepack
cd ..

cd Nethereum.Merkle.Patricia
CALL :restorepack
cd ..

cd Nethereum.RPC.Extensions
CALL :restorepack
cd ..

cd Nethereum.EVM.Contracts
CALL :restorepack
cd ..

cd Nethereum.DataServices
CALL :restorepack
cd ..

cd Nethereum.WalletConnect
CALL :restorepack
cd ..

cd Nethereum.Mud
CALL :restorepack
cd ..

cd Nethereum.Mud.Contracts
CALL :restorepack
cd ..

cd Nethereum.Mud.Repositories.Postgres
CALL :restorepack
cd ..

cd Nethereum.Mud.Repositories.EntityFramework
CALL :restorepack
cd ..

cd Nethereum.Util.Rest
CALL :restorepack
cd ..

setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0packages\
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages /y

for /R %DIR% %%a in (*.snupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.snupkg packages /y

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore %projectName% /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet35=%targetNet35%
dotnet build %projectName% -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
dotnet pack %projectName% -c Release --include-symbols -p:SymbolPackageFormat=snupkg /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
EXIT /B 0