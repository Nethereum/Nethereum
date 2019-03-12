rem packing code generators
del /S *.*.nupkg
del /S *.*.snupkg

cd Nethereum.Generator.Console
CALL :restorepack
cd ..

cd Nethereum.Generators
CALL :restorepack
cd ..

cd Nethereum.Generators.Net
CALL :restorepack
cd ..

setlocal
set DIR=%~dp0
set OUTPUTDIR=%~dp0\packages-generators
for /R %DIR% %%a in (*.nupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.nupkg packages-generators /s /y


for /R %DIR% %%a in (*.snupkg) do xcopy "%%a" "%OUTPUTDIR%"
xcopy *.snupkg packages /s /y

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore -c Release
dotnet pack -c Release --include-symbols -p:SymbolPackageFormat=snupkg
EXIT /B 0