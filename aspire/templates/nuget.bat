@echo off
SET version=1.0.0

echo Packing Nethereum.Aspire.TemplatePack v%version%...

del /S *.nupkg 2>nul

dotnet pack Nethereum.Aspire.TemplatePack\Nethereum.Aspire.TemplatePack.csproj -c Release /p:PackageVersion=%version% -o .

echo.
echo Done. Package:
dir /B *.nupkg 2>nul
