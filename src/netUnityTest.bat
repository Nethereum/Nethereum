rem packing web3 and dependencies
del /S compiledlibraries\net472dllsAOT\*.dll
SET releaseSuffix=
SET targetNet35=false

cd Nethereum.Unity
CALL :build
cd ..

cd Nethereum.Unity.Metamask
CALL :build
cd ..



EXIT /B %ERRORLEVEL%

:build
rem dotnet clean /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
rem  dotnet restore /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
dotnet build  -c Release /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet472=true /property:TargetNet35=false /property:TargetUnityNet472AOT=true
xcopy bin\Release\net472\*.dll "..\compiledlibraries\net472dllsAOT" /s /y
xcopy bin\Release\net472\*.jslib "..\compiledlibraries\net472dllsAOT" /s /y
EXIT /B 0