rem packing code generators
del /S *.*.nupkg

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

EXIT /B %ERRORLEVEL%

:restorepack
dotnet restore
dotnet pack
EXIT /B 0