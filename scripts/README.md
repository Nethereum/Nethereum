# Herumi BLS Build Helpers

This folder contains the helper scripts for compiling and staging the native Herumi `bls_eth` libraries under `src/Nethereum.Signer.Bls.Herumi/runtimes/<rid>/native/`.

## Windows

The Windows build relies on MSVC (`cl`, `link`, `ml64`). **Always** run the PowerShell helper from a Visual Studio “x64 Native Tools Command Prompt” (or after invoking `vcvars64.bat`) so those tools are on your `PATH`:

```powershell
pwsh -ExecutionPolicy Bypass -File scripts\build-herumi-bls.ps1
```

This updates submodules, runs `mklib dll eth`, and copies `bin\bls384_256.dll` to `src\Nethereum.Signer.Bls.Herumi\runtimes\win-x64\native\bls_eth.dll`.

## Linux/macOS

Use the bash helper (requires a working C++ toolchain and Herumi prerequisites):

```bash
bash scripts/build-herumi-bls.sh
```

It runs the same steps and drops `libbls_eth.so` (and, once enabled, `libbls_eth.dylib`) into the matching RID folders.
