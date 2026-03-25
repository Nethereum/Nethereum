# Nethereum.CircomWitnessCalc

Native circom witness generation for .NET using [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc). Computes witnesses from circom circuit graphs without JavaScript or Node.js — works on desktop, server, and mobile.

Companion to [Nethereum.ZkProofs.RapidSnark](../Nethereum.ZkProofs.RapidSnark/) for a fully native ZK proof pipeline.

## How It Works

Circom circuits are normally executed via JavaScript (snarkjs + Node.js). This package replaces that with a native C library:

1. **Compile** your `.circom` circuit into a binary graph (`.graph.bin`) using `build-circuit` (one-time)
2. **At runtime**, pass the graph + JSON inputs to `WitnessCalculator.CalculateWitness()` which returns the witness bytes (`.wtns` format)
3. **Feed the witness** to [Nethereum.ZkProofs.RapidSnark](../Nethereum.ZkProofs.RapidSnark/) for fast native proof generation

```
                          ONE-TIME (build step)
    circuit.circom  ──>  build-circuit --O1  ──>  circuit.graph.bin
                                                       |
                          PER-REQUEST (runtime)        |
    input JSON  ──>  WitnessCalculator  ──>  witness.wtns  ──>  RapidSnark  ──>  proof.json
                     (this package)                             (ZkProofs.RapidSnark)
```

No Node.js, no JavaScript, no WASM runtime. Pure native code on all platforms.

## Usage

### Direct witness calculation

```csharp
byte[] graphData = File.ReadAllBytes("circuit.graph.bin");
string inputsJson = """{"nullifier":"123","secret":"456","value":"1000","label":"1"}""";

byte[] witnessBytes = WitnessCalculator.CalculateWitness(graphData, inputsJson);
// witnessBytes is standard .wtns format, compatible with rapidsnark and snarkjs
```

### Combined with RapidSnark (full native pipeline)

```csharp
// NativeProofProvider handles both witness generation and proving
var provider = new NativeProofProvider();

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitZkey = File.ReadAllBytes("circuit.zkey"),
    CircuitGraph = File.ReadAllBytes("circuit.graph.bin"),
    InputJson = inputsJson,
});
// result.ProofJson, result.PublicSignalsJson
```

### With embedded circuit artifacts (e.g. Privacy Pools)

```csharp
// PrivacyPoolCircuitSource embeds .zkey + .graph.bin as resources
var circuitSource = new PrivacyPoolCircuitSource();
var proofProvider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);

var result = await proofProvider.GenerateRagequitProofAsync(witnessInput);
```

## Supported Platforms

Pre-built native binaries are included for all platforms (built via [Nethereum/circom-witnesscalc CI](https://github.com/Nethereum/circom-witnesscalc/actions)):

| Platform | Architecture | File | Size |
|----------|-------------|------|------|
| Windows | x64 | `circom_witnesscalc.dll` | 512 KB |
| Linux | x64 | `libcircom_witnesscalc.so` | 900 KB |
| Linux | arm64 | `libcircom_witnesscalc.so` | 855 KB |
| macOS | x64 | `libcircom_witnesscalc.dylib` | 743 KB |
| macOS | arm64 (Apple Silicon) | `libcircom_witnesscalc.dylib` | 745 KB |
| Android | arm64 | `libcircom_witnesscalc.so` | 919 KB |
| iOS | arm64 | `libcircom_witnesscalc.a` | via CI |

## Generating Circuit Graphs

Circuit graphs are compiled from `.circom` source using the `build-circuit` tool from [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc).

### Install build-circuit

```bash
git clone https://github.com/iden3/circom-witnesscalc.git
cd circom-witnesscalc
cargo build --release -p build-circuit
# Output: target/release/build-circuit (.exe on Windows)
```

Build dependencies: Rust toolchain, protoc, LLVM/clang. See [Building from source](#building-from-source) below.

### Compile a circuit

```bash
build-circuit circuit.circom circuit.graph.bin -l node_modules/ --O1
```

**Important: use `--O1` optimization.** The default `--O2` over-optimizes and reduces signal count below what the zkey expects. `--O1` matches circom's default optimization used during trusted setup.

If your circuit uses circomkit (no inline `component main`), create a wrapper:

```circom
pragma circom 2.2.0;
include "myCircuit.circom";
component main {public [input1, input2]} = MyTemplate();
```

### Verify signal count matches zkey

```bash
circom circuit_main.circom --r1cs -l node_modules/
snarkjs r1cs info circuit_main.r1cs
# Check "# of Wires" matches what your zkey expects
```

If mismatched, rapidsnark will error: `Invalid witness length. Circuit: <expected>, witness: <actual>`.

## Updating Native Binaries

Native binaries are built by the CI workflow at [Nethereum/circom-witnesscalc](https://github.com/Nethereum/circom-witnesscalc/actions):

1. Push or create a release on the fork
2. Download artifacts from the workflow run
3. Copy shared libraries to `runtimes/<rid>/native/`

```bash
# Download all artifacts from a CI run
gh run download <run-id> -R Nethereum/circom-witnesscalc

# Copy to package runtimes
cp circom-witnesscalc-win-x64/circom_witnesscalc.dll     runtimes/win-x64/native/
cp circom-witnesscalc-linux-x64/libcircom_witnesscalc.so  runtimes/linux-x64/native/
cp circom-witnesscalc-linux-arm64/libcircom_witnesscalc.so runtimes/linux-arm64/native/
cp circom-witnesscalc-osx-arm64/libcircom_witnesscalc.dylib runtimes/osx-arm64/native/
cp circom-witnesscalc-osx-x64/libcircom_witnesscalc.dylib runtimes/osx-x64/native/
cp circom-witnesscalc-android-arm64/libcircom_witnesscalc.so runtimes/android-arm64/native/
```

## Building from Source

### All platforms (CI)

The [Nethereum/circom-witnesscalc](https://github.com/Nethereum/circom-witnesscalc) fork has a GitHub Actions workflow that builds for Windows, Linux (x64/arm64), macOS (x64/arm64), Android (arm64/x64), and iOS (arm64/sim).

### Windows (local)

Prerequisites: Rust toolchain, protoc, LLVM/clang.

```bash
# Install protoc
choco install protoc
# or download: https://github.com/protocolbuffers/protobuf/releases

# Install LLVM (for libclang)
winget install LLVM.LLVM
# or choco install llvm

# Clone and build
git clone https://github.com/iden3/circom-witnesscalc.git
cd circom-witnesscalc
set LIBCLANG_PATH=C:\Program Files\LLVM\bin
cargo build --release
```

Output: `target/release/circom_witnesscalc.dll` (512 KB). Depends only on standard Windows system DLLs.

### Linux

```bash
sudo apt-get install -y protobuf-compiler libclang-dev
git clone https://github.com/iden3/circom-witnesscalc.git
cd circom-witnesscalc
cargo build --release
```

### macOS

```bash
brew install protobuf
git clone https://github.com/iden3/circom-witnesscalc.git
cd circom-witnesscalc
cargo build --release
```

## C API

The library exports a single function (`include/graph_witness.h`):

```c
typedef struct {
  int code;           // 0 = OK, 1 = ERROR
  char *error_msg;    // error message (caller must free)
} gw_status_t;

int gw_calc_witness(
    const char *inputs,              // JSON string of circuit inputs
    const void *graph_data,          // binary circuit graph bytes
    const size_t graph_data_len,     // graph length
    void **wtns_data,                // OUT: witness .wtns bytes (caller must free)
    size_t *wtns_len,                // OUT: witness length
    const gw_status_t *status        // OUT: error status
);
```

## Credits

- [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) — Native witness calculator (Rust with C FFI)
- [iden3/circom](https://github.com/iden3/circom) — Circom circuit compiler
- [iden3/rapidsnark](https://github.com/iden3/rapidsnark) — Native Groth16 prover
