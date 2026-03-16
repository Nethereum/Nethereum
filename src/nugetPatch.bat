@echo off
rem Pack specific projects with a patch version suffix
rem Usage: nugetPatch.bat [suffix]
rem Example: nugetPatch.bat patch1
rem Produces: Nethereum.DevChain.6.0.0-patch1.nupkg

SET releaseSuffix=%1
SET targetNet35=false
SET projectName=

if "%releaseSuffix%"=="" (
    echo Usage: nugetPatch.bat [suffix]
    echo Example: nugetPatch.bat patch1
    EXIT /B 1
)

echo Packing DevChain and DevChain.Server with suffix: %releaseSuffix%

cd Nethereum.DevChain
CALL :restorepack
cd..

cd Nethereum.DevChain.Server
CALL :restorepack
cd..

setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0packages\
if not exist "%OUTPUTDIR%" mkdir "%OUTPUTDIR%"
for /R %DIR% %%a in (*%releaseSuffix%.nupkg) do xcopy "%%a" "%OUTPUTDIR%" /y
for /R %DIR% %%a in (*%releaseSuffix%.snupkg) do xcopy "%%a" "%OUTPUTDIR%" /y

echo.
echo Done. Packages in %OUTPUTDIR%:
dir /B "%OUTPUTDIR%*%releaseSuffix%*" 2>nul

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore %projectName% /property:ReleaseSuffix=%releaseSuffix% /property:TargetNet35=%targetNet35%
dotnet build %projectName% -c Release /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
dotnet pack %projectName% -c Release --include-symbols -p:SymbolPackageFormat=snupkg /property:TargetNet35=%targetNet35% /property:ReleaseSuffix=%releaseSuffix%
EXIT /B 0
