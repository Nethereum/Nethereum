# Nethereum.Signer.Bls.Herumi

Native Herumi BLS implementation for Ethereum consensus layer (Beacon Chain) signature verification.

## Overview

Nethereum.Signer.Bls.Herumi provides a **production-ready native implementation** of BLS (Boneh-Lynn-Shacham) signatures using the Herumi BLS library. This is the recommended implementation for verifying Ethereum consensus layer signatures (sync committees, validator attestations, light clients).

**Key Features:**
- Native Herumi BLS library (MCL/BLST backend)
- High-performance BLS12-381 operations
- Cross-platform support (Windows, Linux, macOS)
- Ethereum 2.0 sync committee verification
- Light client signature verification
- Production-tested in Ethereum infrastructure

**Use Cases:**
- Light clients (verify beacon chain without full node)
- Sync committee signature verification
- Consensus layer data validation
- Portal Network implementations
- Ethereum beacon chain APIs

## Installation

```bash
dotnet add package Nethereum.Signer.Bls.Herumi
```

**Platform Support:**
- Windows (x64)
- Linux (x64)

## Dependencies

**Nethereum:**
- **Nethereum.Signer.Bls** - Core BLS abstraction

**Native Libraries** (included in package):
- Herumi BLS native binaries for all supported platforms

## Quick Start

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

// Create Herumi BLS instance
var blsBindings = new HerumiNativeBindings();
var bls = new NativeBls(blsBindings);

// Initialize (loads native library)
await bls.InitializeAsync();

// Verify aggregate BLS signature
bool isValid = bls.VerifyAggregate(
    aggregateSignature,  // 96 bytes
    publicKeys,          // Array of 48-byte validator public keys
    messages,            // Array of 32-byte signing roots
    domain               // 32 bytes: forkDigest|domainType
);

Console.WriteLine($"Signature valid: {isValid}");
```

## API Reference

### NativeBls

High-level BLS operations implementing `IBls`. This is the main entry point for consumers.

```csharp
public class NativeBls : IBls
{
    public NativeBls(INativeBlsBindings bindings);
    public Task InitializeAsync(CancellationToken cancellationToken = default);
    public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);
    public byte[] AggregateSignatures(byte[][] signatures);
    public bool Verify(byte[] signature, byte[] publicKey, byte[] message);
    public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey);
}
```

- `VerifyAggregate` — verifies an aggregate BLS signature over one or more messages using ETH2-style domain separation.
- `AggregateSignatures` — combines multiple BLS signatures into a single aggregated signature (used for ERC-4337 BLS aggregation).
- `Verify` — verifies an individual BLS signature over a single message.
- `ExtractSignatureAndPublicKey` — splits a combined `signatureWithPubKey` byte array into the 96-byte signature and 48-byte public key.

### HerumiNativeBindings

Low-level native Herumi BLS bindings implementing `INativeBlsBindings`. Used internally by `NativeBls`.

```csharp
public class HerumiNativeBindings : INativeBlsBindings
{
    public Task EnsureAvailableAsync(CancellationToken cancellationToken = default);
    public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);
    public byte[] AggregateSignatures(byte[][] signatures);
    public bool Verify(byte[] signature, byte[] publicKey, byte[] message);
}
```

## Important Notes

### BLS12-381 Curve

- **Curve**: BLS12-381 (NOT secp256k1)
- **Public Key**: 48 bytes (compressed G1 point)
- **Signature**: 96 bytes (compressed G2 point)
- **Security Level**: ~128-bit (equivalent to 3072-bit RSA)

### Domain Separation

Ethereum consensus layer uses domain types to prevent cross-context signature replay. The domain is a 4-byte prefix combined with the fork version and genesis validators root. Standard domain type prefixes:

| Domain | Prefix | Usage |
|--------|--------|-------|
| Beacon Proposer | `0x00000000` | Block proposals |
| Beacon Attester | `0x01000000` | Attestations |
| RANDAO | `0x02000000` | Randomness reveals |
| Sync Committee | `0x07000000` | Light client sync |

These domain bytes are passed to BLS signing/verification as the `domain` parameter.

### Native Library Loading

The package includes native binaries in the `runtimes/` folder:

```
runtimes/
  win-x64/native/bls_eth.dll
  win-x64/native/mcl.dll
  linux-x64/native/libbls_eth.so
```

.NET automatically loads the correct native library for your platform.

### Performance

| Operation | Time | Notes |
|-----------|------|-------|
| **Init** | ~10ms | One-time initialization |
| **Verify 1 sig** | ~5ms | Single signature |
| **Verify 512 aggregate** | ~70ms | Sync committee (modern CPU) |
| **Verify 1000 aggregate** | ~130ms | Large aggregation |

**Optimization Tips:**
- Initialize once and reuse `NativeBls` instance
- Aggregate verification is much faster than N individual verifications
- Use cached domain values

### Thread Safety

`NativeBls` is **thread-safe** after initialization:

```csharp
// Initialize once
var bls = new NativeBls(new HerumiNativeBindings());
await bls.InitializeAsync();

// Safe to use from multiple threads
Parallel.For(0, 100, i =>
{
    var isValid = bls.VerifyAggregate(...);
});
```

### Error Handling

```csharp
try
{
    await bls.InitializeAsync();
}
catch (DllNotFoundException ex)
{
    // Native library not found for platform
    Console.WriteLine($"Platform not supported: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // BLS library initialization failed
    Console.WriteLine($"BLS init failed: {ex.Message}");
}

// Verification errors
try
{
    var isValid = bls.VerifyAggregate(...);
}
catch (ArgumentException ex)
{
    // Invalid input (wrong sizes, null arrays)
    Console.WriteLine($"Invalid input: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Not initialized
    Console.WriteLine($"Call InitializeAsync first: {ex.Message}");
}
```

## Platform-Specific Notes

### Windows (x64)
- Requires Visual C++ Redistributable (usually pre-installed)
- Includes both `bls_eth.dll` and `mcl.dll`

### Linux (x64)
- Works on most distros (Ubuntu, Debian, Fedora, Alpine)
- May require `libstdc++6` on some systems
- Uses `libbls_eth.so`

## Related Packages

### Dependencies
- **Nethereum.Signer.Bls** - Core BLS abstraction

### Used By
- **Nethereum.Consensus.Ssz** - SSZ encoding
- Light client implementations
- Beacon chain verification tools

## Developer Guide: Native Library Packaging

This package uses a **local NuGet package** for native library distribution. This ensures native assets are correctly deployed on all platforms (Windows, Linux, Android, etc.).

### Why Local Package?

Native libraries in `runtimes/<rid>/native/` only work correctly when distributed via NuGet package. Using `ProjectReference` does not properly copy native assets to consuming Android/iOS apps.

### Package Workflow

**After ANY change to native libraries**, you must re-pack:

```bash
# From repo root
dotnet pack src/Nethereum.Signer.Bls.Herumi/Nethereum.Signer.Bls.Herumi.csproj -o nativeartifacts -c Release
```

This creates/updates `nativeartifacts/Nethereum.Signer.Bls.Herumi.{version}.nupkg`.

### Local Feed Configuration

The repo has a `NuGet.config` at root that includes the local feed:

```xml
<configuration>
  <packageSources>
    <add key="local" value="./nativeartifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### Dependency Chain

```
Any consumer app
    → Nethereum.Wallet
        → Nethereum.Signer.Bls.Herumi (PackageReference)
            → runtimes/win-x64/native/bls_eth.dll
            → runtimes/linux-x64/native/libbls_eth.so
            → runtimes/android-arm64/native/libbls_eth.so
```

`Nethereum.Wallet` references this package via `PackageReference` (not `ProjectReference`) so native assets flow correctly.

### Native Library Locations

```
runtimes/
├── win-x64/native/
│   ├── bls_eth.dll
│   └── mcl.dll
├── linux-x64/native/
│   └── libbls_eth.so
└── android-arm64/native/
    └── libbls_eth.so
```

### Building Native Libraries

#### Windows (x64)

Pre-built from Herumi releases or build with MSVC. Requires both `bls_eth.dll` and `mcl.dll`.

#### Linux (x64)

Build in WSL or Linux:
```bash
cd external/bls
make BLS_ETH=1
cp lib/libbls_eth.so /path/to/runtimes/linux-x64/native/
```

#### Android (ARM64) - Recommended Method

Use pre-compiled static libraries from [bls-eth-go-binary](https://github.com/herumi/bls-eth-go-binary) and create a shared library:

```bash
# Step 1: Clone the release branch (contains pre-compiled static libs)
cd /tmp
git clone -b release https://github.com/herumi/bls-eth-go-binary
cd bls-eth-go-binary

# Step 2: Verify static library exists
ls -la bls/lib/android/arm64-v8a/
# Expected: libbls384_256.a

# Step 3: Set up Android NDK
export ANDROID_NDK_HOME=/path/to/android-ndk  # e.g., ~/Android/Sdk/ndk/26.1.10909125
export CXX=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android21-clang++

# Step 4: Create shared library from static library
# Use -static-libstdc++ to avoid libc++_shared.so dependency
$CXX -shared -static-libstdc++ -o libbls_eth.so \
    -Wl,--whole-archive bls/lib/android/arm64-v8a/libbls384_256.a -Wl,--no-whole-archive

# Step 5: Verify no C++ runtime dependency
readelf -d libbls_eth.so | grep NEEDED
# Should only show: libm.so, libdl.so, libc.so (NO libc++_shared.so)

# Step 6: Copy to runtimes folder
cp libbls_eth.so /path/to/Nethereum/src/Nethereum.Signer.Bls.Herumi/runtimes/android-arm64/native/
```

**Why use pre-compiled static libs?**
- Built by Herumi with correct `BLS_ETH=1` configuration
- `COMPILED_TIME_VAR` (246) is baked in at compile time
- The `--whole-archive` flag ensures all symbols are included

#### Android (ARM64) - Build from Source (Alternative)

If pre-compiled doesn't work, build from source. **Important:** Do NOT define `MCL_FP_BIT` or `MCL_FR_BIT` on command line - let headers set them via `bn_c384_256.h`.

```bash
export CC=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android21-clang
export CXX=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android21-clang++
export AR=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-ar

cd external/bls

MCL_FLAGS="-O3 -DNDEBUG -DMCL_DONT_USE_XBYAK -DMCL_VINT_FIXED_BUFFER -DMCL_DONT_USE_OPENSSL -DMCL_USE_VINT -DMCL_VINT_64BIT_PORTABLE -DMCL_FP_GENERIC -DMCL_SIZEOF_UNIT=8 -fPIC"

mkdir -p obj lib mcl/obj mcl/lib

$CXX -c $MCL_FLAGS -I mcl/include -o mcl/obj/fp.o mcl/src/fp.cpp
$CXX -c $MCL_FLAGS -I mcl/include -o mcl/obj/bn_c384_256.o mcl/src/bn_c384_256.cpp
$CXX -c -DBLS_ETH=1 -DBLS_DLL_EXPORT -fvisibility=default $MCL_FLAGS -I include -I mcl/include -o obj/bls_c384_256.o src/bls_c384_256.cpp
$CXX -shared -static-libstdc++ -o lib/libbls_eth.so obj/bls_c384_256.o mcl/obj/fp.o mcl/obj/bn_c384_256.o

cp lib/libbls_eth.so /path/to/runtimes/android-arm64/native/
```

### Checklist: After Native Library Changes

1. [ ] Copy new `.dll`/`.so` files to appropriate `runtimes/<rid>/native/` folder
2. [ ] Run `dotnet pack src/Nethereum.Signer.Bls.Herumi -o nativeartifacts -c Release`
3. [ ] Verify package contents: `unzip -l nativeartifacts/Nethereum.Signer.Bls.Herumi.*.nupkg | grep runtimes`
4. [ ] Run `dotnet restore` in consuming projects
5. [ ] Test on target platforms

### Android Native Library Loading Architecture

On Android, .NET's `DllImport("bls_eth")` doesn't automatically resolve to `libbls_eth.so` like it does on Linux/Windows. This package uses a `NativeLibrary.SetDllImportResolver` to handle cross-platform library resolution.

**How it works:**

1. **`BlsNativeLibraryResolver.cs`** - Registered via `[ModuleInitializer]` when the assembly loads
2. Intercepts DllImport calls for `"bls_eth"`
3. Resolves to platform-specific names:
   - Android: `libbls_eth.so` or `libbls_eth`
   - Windows: `bls_eth.dll`
   - Linux: `libbls_eth.so`
   - macOS: `libbls_eth.dylib`

**Key files:**

```
Nethereum.Signer.Bls.Herumi/
├── BlsNativeLibraryResolver.cs    # [ModuleInitializer] - auto-registers resolver
├── bls_eth.cs                     # DllImport declarations (uses "bls_eth")
├── HerumiNativeBindings.cs        # High-level API
├── buildTransitive/
│   └── Nethereum.Signer.Bls.Herumi.targets  # Registers AndroidNativeLibrary
└── runtimes/
    ├── android-arm64/native/libbls_eth.so
    ├── linux-x64/native/libbls_eth.so
    └── win-x64/native/bls_eth.dll, mcl.dll
```

**The `buildTransitive/*.targets` file** ensures Android includes the native library in the APK:

```xml
<Project>
  <ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <AndroidNativeLibrary Include="...\runtimes\android-arm64\native\libbls_eth.so" Abi="arm64-v8a" />
  </ItemGroup>
</Project>
```

This places `libbls_eth.so` at `lib/arm64-v8a/libbls_eth.so` in the APK.

### Troubleshooting

**"Library not found" on Android:**
- Verify `libbls_eth.so` is in package under `runtimes/android-arm64/native/`
- Check APK contains `lib/arm64-v8a/libbls_eth.so`: `unzip -l app.apk | grep libbls_eth`
- Ensure `buildTransitive/*.targets` is included in the NuGet package

**"blsInit failed" / COMPILED_TIME_VAR mismatch:**
- Library must be built with `BLS_ETH=1` flag
- C# expects `COMPILED_TIME_VAR=246` (BLS_ETH mode)
- If built without BLS_ETH, it expects 46 and will fail
- Use pre-compiled static libs from `bls-eth-go-binary` to ensure correct build

**Package not found during restore:**
- Ensure `NuGet.config` exists at repo root with local feed
- Run pack command first to create the package
- Clear NuGet cache if needed: `rm -rf ~/.nuget/packages/nethereum.signer.bls.herumi`

## Additional Resources

- [Herumi BLS Library](https://github.com/herumi/bls)
- [BLS12-381 Specification](https://hackmd.io/@benjaminion/bls12-381)
- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs)
- [Sync Committee Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/sync-protocol.md)
- [Nethereum Documentation](http://docs.nethereum.com/)
