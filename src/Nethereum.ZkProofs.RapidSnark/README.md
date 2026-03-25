# Nethereum.ZkProofs.RapidSnark

Native Groth16 proof generation for .NET using [iden3/rapidsnark](https://github.com/iden3/rapidsnark). Significantly faster than snarkjs — no Node.js dependency.

Pair with [Nethereum.CircomWitnessCalc](../Nethereum.CircomWitnessCalc/) for a fully native ZK proof pipeline (witness generation + proving, zero JavaScript).

## How It Works

RapidSnark is a fast C++ Groth16 prover. It takes a **proving key** (`.zkey`) and a **witness** (`.wtns`) and outputs a proof. It does NOT generate witnesses — that's handled by [Nethereum.CircomWitnessCalc](../Nethereum.CircomWitnessCalc/).

This package provides three levels of API:

| Class | What it does | When to use |
|-------|-------------|-------------|
| `NativeProofProvider` | Full pipeline: witness + proof (implements `IZkProofProvider`) | Drop-in replacement for `SnarkjsProofProvider` |
| `RapidSnarkProofProvider` | Proof from pre-computed witness (implements `IZkProofProvider`) | When you generate witnesses separately |
| `RapidSnarkProver` | Low-level P/Invoke prover with reusable zkey | Maximum performance, batch proving |

## Usage

### NativeProofProvider (recommended — full pipeline, no JS)

Drop-in replacement for `SnarkjsProofProvider`. Handles witness generation (via CircomWitnessCalc) and proof generation (via RapidSnark) in one call.

```csharp
var provider = new NativeProofProvider();

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitZkey = File.ReadAllBytes("circuit.zkey"),
    CircuitGraph = File.ReadAllBytes("circuit.graph.bin"),  // from build-circuit --O1
    InputJson = """{"nullifier":"123","secret":"456","value":"1000","label":"1"}""",
});

Console.WriteLine(result.ProofJson);
Console.WriteLine(result.PublicSignalsJson);
```

### With embedded circuit artifacts

When your circuit source provides both `ICircuitArtifactSource` and `ICircuitGraphSource` (e.g. `PrivacyPoolCircuitSource`), the proof provider automatically uses the graph for native witness generation:

```csharp
var circuitSource = new PrivacyPoolCircuitSource(); // embeds .zkey + .graph.bin
var proofProvider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);

// Fully native — no Node.js, no JavaScript
var result = await proofProvider.GenerateRagequitProofAsync(witnessInput);
```

### RapidSnarkProofProvider (pre-computed witness)

When you already have the witness bytes (e.g. from an external witness generator):

```csharp
var provider = new RapidSnarkProofProvider();

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitZkey = zkeyBytes,
    WitnessBytes = witnessBytes,  // .wtns binary
});
```

### RapidSnarkProver (low-level, reusable zkey)

For batch proving — load the zkey once, prove many times:

```csharp
using var prover = new RapidSnarkProver();
prover.LoadZkey(zkeyBytes);

var (proof1, public1) = prover.ProveWithLoadedZkey(witness1);
var (proof2, public2) = prover.ProveWithLoadedZkey(witness2);
var (proof3, public3) = prover.ProveWithLoadedZkey(witness3);
```

### Native verification

```csharp
bool valid = RapidSnarkVerifier.Verify(proofJson, publicSignalsJson, verificationKeyJson);
```

## Supported Platforms

Pre-built native binaries included (from [Nethereum/rapidsnark CI](https://github.com/Nethereum/rapidsnark/actions) and [iden3/rapidsnark releases v0.0.8](https://github.com/iden3/rapidsnark/releases/tag/v0.0.8)):

| Platform | Architecture | File | Size |
|----------|-------------|------|------|
| Windows | x64 | `rapidsnark.dll` | 3.9 MB |
| Linux | x64 | `librapidsnark.so` | 970 KB |
| Linux | arm64 | `librapidsnark.so` | 780 KB |
| macOS | x64 | `librapidsnark.dylib` | 684 KB |
| macOS | arm64 (Apple Silicon) | `librapidsnark.dylib` | 620 KB |
| Android | arm64 | `librapidsnark.so` | 2.2 MB |

## Performance

Measured on Windows x64 with a Groth16 circuit (~3,800 constraints):

| Provider | Time | Notes |
|----------|------|-------|
| SnarkjsProofProvider (Node.js) | ~1,638ms | Witness + proof via Node.js |
| NativeProofProvider | ~128ms | Witness (circom-witnesscalc) + proof (rapidsnark) |
| RapidSnarkProver (zkey pre-loaded) | ~96-105ms | Proof only, zkey cached |

Both providers produce identical public signals and both proofs verify via Groth16 BN128 pairing check.

## Updating Native Binaries

Native binaries are built by the CI workflow at [Nethereum/rapidsnark](https://github.com/Nethereum/rapidsnark/actions) (the `windows-support` branch adds Windows builds to the upstream CI):

```bash
# Download all artifacts from a CI run
gh run download <run-id> -R Nethereum/rapidsnark

# Copy to package runtimes
cp rapidsnark-windows-x86_64/lib/librapidsnark.dll  runtimes/win-x64/native/rapidsnark.dll
cp rapidsnark-linux-x86_64/lib/librapidsnark.so     runtimes/linux-x64/native/
cp rapidsnark-linux-arm64/lib/librapidsnark.so       runtimes/linux-arm64/native/
cp rapidsnark-macOS-arm64/lib/librapidsnark.dylib    runtimes/osx-arm64/native/
cp rapidsnark-macOS-x86_64/lib/librapidsnark.dylib   runtimes/osx-x64/native/
cp rapidsnark-Android/lib/librapidsnark.so            runtimes/android-arm64/native/
```

## Building from Source

### All platforms (CI)

The [Nethereum/rapidsnark](https://github.com/Nethereum/rapidsnark) fork (`windows-support` branch) has a GitHub Actions workflow that builds for Windows, Linux (x64/arm64), macOS (x64/arm64), Android, and iOS. See `WINDOWS_BUILD.md` in that repo for full details.

### Windows (MSYS2/MinGW64)

```bash
# Install MSYS2
winget install MSYS2.MSYS2

# In MSYS2 MinGW64 shell:
pacman -S --noconfirm mingw-w64-x86_64-gcc mingw-w64-x86_64-cmake make m4 diffutils tar xz curl

# Clone and build
git clone --recursive https://github.com/Nethereum/rapidsnark.git
cd rapidsnark
git checkout windows-support
./build_gmp.sh windows
make windows_x86_64
# Output: package_windows_x86_64/lib/librapidsnark.dll
```

### Linux / macOS

```bash
git clone --recursive https://github.com/iden3/rapidsnark.git
cd rapidsnark
bash build_gmp.sh host        # or macos_arm64, macos_x86_64
make host                     # or macos_arm64, macos_x86_64
```

## Package Relationship

```
Nethereum.CircomWitnessCalc          Nethereum.ZkProofs.RapidSnark (this)
  |                                    |
  |  graph + inputs --> witness        |  zkey + witness --> proof
  |  (circom-witnesscalc native)       |  (rapidsnark native)
  |                                    |
  +-------- NativeProofProvider -------+
            (combines both into IZkProofProvider)
```

| Package | Source | Role |
|---------|--------|------|
| [Nethereum.CircomWitnessCalc](../Nethereum.CircomWitnessCalc/) | [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) | Native witness generation |
| **Nethereum.ZkProofs.RapidSnark** (this) | [iden3/rapidsnark](https://github.com/iden3/rapidsnark) | Native proof generation |
| [Nethereum.ZkProofs](../Nethereum.ZkProofs/) | — | `IZkProofProvider` interface + `ZkProofRequest`/`ZkProofResult` |

## Credits

- [iden3/rapidsnark](https://github.com/iden3/rapidsnark) — Fast native Groth16 prover (C++)
- [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) — Native witness calculator (Rust with C FFI)
- [iden3/circom](https://github.com/iden3/circom) — Circom circuit compiler
