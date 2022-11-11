rem packing web3 and dependencies
del /S ..\FxOutput\*.*.nupkg
del /S ..\FxOutput\*.*.snupkg
del /S packagesFx\*.*.nupkg
del /S packagesFx\*.*.snupkg

SET releaseSuffix=
SET targetNet35=false
SET projectName=

SET projectName=Nethereum.Fx.csproj
CALL :restorepack

cd ..\FxOutput\Nethereum.LiteFx
SET projectName=Nethereum.LiteFx.csproj
CALL :restorepack
cd ..

setlocal
set DIR=%~dp0..\FxOutput
set OUTPUTDIR=%~dp0\packagesFx
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
for /R %DIR% %%a in (*.snupkg) do xcopy "%%a" "%OUTPUTDIR%"

cd ../src

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore %projectName% /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet35=%targetNet35%
dotnet build %projectName% -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
dotnet pack %projectName% -c Release --include-symbols -p:SymbolPackageFormat=snupkg /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
EXIT /B 0