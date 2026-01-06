@echo off
echo ========================================
echo Building Nethereum.Signer.Bls.Herumi first
echo ========================================

cd Nethereum.Signer.Bls.Herumi
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Restore failed
    cd ..
    EXIT /B %ERRORLEVEL%
)

dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed
    cd ..
    EXIT /B %ERRORLEVEL%
)

dotnet pack -c Release --include-symbols -p:SymbolPackageFormat=snupkg
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Pack failed
    cd ..
    EXIT /B %ERRORLEVEL%
)

echo.
echo Copying packages to nativeartifacts folder...
copy /Y bin\Release\*.nupkg ..\..\nativeartifacts\
copy /Y bin\Release\*.snupkg ..\..\nativeartifacts\

cd ..

echo.
echo ========================================
echo Herumi package built and copied successfully!
echo You can now run nuget.bat
echo ========================================
