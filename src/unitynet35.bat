rem packing unity dlls
del /S compiledlibraries\unitynet35dlls\*.dll
SET releaseSuffix=

cd Nethereum.ABI
CALL :build
cd ..

cd Nethereum.Contracts*
CALL :build
cd ..

cd Nethereum.Hex
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

cd Nethereum.KeyStore*
CALL :build
cd ..

cd Nethereum.RLP*
CALL :build
cd ..

cd Nethereum.Signer*
CALL :build
cd ..

cd Nethereum.Util*
CALL :build
cd ..

cd Nethereum.Accounts*
CALL :build
cd ..

cd Nethereum.Unity*
CALL :build
cd ..


EXIT /B %ERRORLEVEL%

:build
dotnet build -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet461=false /property:TargetNet35=true
xcopy bin\Release\net35\*.dll "..\compiledlibraries\unitynet35dlls" /s /y
EXIT /B 0



