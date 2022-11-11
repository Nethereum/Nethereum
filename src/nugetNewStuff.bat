rem packing web3 and dependencies

SET releaseSuffix=
SET targetNet35=false
SET projectName=


cd Nethereum.Optimism
CALL :restorepack
cd ..

setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0\packages
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages /s /y

for /R %DIR% %%a in (*.snupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.snupkg packages /s /y

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore %projectName% /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet35=%targetNet35%
dotnet build %projectName% -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
dotnet pack %projectName% -c Release --include-symbols -p:SymbolPackageFormat=snupkg /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
EXIT /B 0