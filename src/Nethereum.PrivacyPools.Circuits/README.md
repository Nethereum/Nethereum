# Nethereum.PrivacyPools.Circuits

Embedded circuit artifacts for the [0xbow Privacy Pools](https://github.com/0xbow-io/privacy-pools-core) protocol. Provides `PrivacyPoolCircuitSource` — implements both `ICircuitArtifactSource` and `ICircuitGraphSource`, loading compiled Circom circuits from embedded resources with no file-system setup.

## Embedded Artifacts

| Circuit | File | Size | Used by |
|---------|------|------|---------|
| commitment | `commitment.wasm` | 2.3 MB | `SnarkjsProofProvider` (Node.js) |
| commitment | `commitment.zkey` | 881 KB | All providers |
| commitment | `commitment.graph.bin` | 68 KB | `NativeProofProvider` (native) |
| commitment | `commitment_vk.json` | 3.4 KB | Verification |
| withdrawal | `withdrawal.wasm` | 2.5 MB | `SnarkjsProofProvider` (Node.js) |
| withdrawal | `withdrawal.zkey` | 17 MB | All providers |
| withdrawal | `withdrawal.graph.bin` | 862 KB | `NativeProofProvider` (native) |
| withdrawal | `withdrawal_vk.json` | 4.2 KB | Verification |

The `.wasm` files are for JavaScript-based proof generation (snarkjs). The `.graph.bin` files are for fully native proof generation (circom-witnesscalc + rapidsnark). Both paths use the same `.zkey` and produce identical proofs.

## Usage

```csharp
var source = new PrivacyPoolCircuitSource();

// Check availability
source.HasCircuit("commitment");   // true — has .wasm, .zkey, .vk
source.HasGraph("commitment");     // true — has .graph.bin

// Load artifacts
byte[] wasm = await source.GetWasmAsync("commitment");       // for snarkjs
byte[] zkey = await source.GetZkeyAsync("commitment");       // for all providers
byte[] graph = source.GetGraphData("commitment");             // for native provider
string vkJson = source.GetVerificationKeyJson("commitment");  // for verification
```

## With Proof Generation

### Native (no JavaScript, no Node.js)

Uses [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) for witness generation and [iden3/rapidsnark](https://github.com/iden3/rapidsnark) for proving. Works on desktop, server, and mobile.

```csharp
var circuitSource = new PrivacyPoolCircuitSource();
var proofProvider = new PrivacyPoolProofProvider(
    new NativeProofProvider(), circuitSource);

var result = await proofProvider.GenerateRagequitProofAsync(
    new RagequitWitnessInput
    {
        Nullifier = commitment.Nullifier,
        Secret = commitment.Secret,
        Value = commitment.Value,
        Label = commitment.Label
    });
```

### Node.js (snarkjs)

```csharp
var circuitSource = new PrivacyPoolCircuitSource();
var proofProvider = new PrivacyPoolProofProvider(
    new SnarkjsProofProvider(), circuitSource);
```

### Blazor WASM (browser)

```csharp
var circuitSource = new PrivacyPoolCircuitSource();
var blazorProvider = new SnarkjsBlazorProvider(jsRuntime, "./js/snarkjs.min.mjs");
await blazorProvider.InitializeAsync();
var proofProvider = new PrivacyPoolProofProvider(blazorProvider, circuitSource);
```

## Compiling Circuit Graph Files

The `.graph.bin` files are pre-compiled and embedded. If you need to recompile them (e.g., after circuit changes), follow these steps.

### Prerequisites

- [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) — build the `build-circuit` tool:

  ```bash
  git clone https://github.com/iden3/circom-witnesscalc.git
  cd circom-witnesscalc
  cargo build --release -p build-circuit
  ```

  See [Nethereum.CircomWitnessCalc README](../Nethereum.CircomWitnessCalc/README.md) for build dependencies (Rust, protoc, LLVM/clang).

- [0xbow-io/privacy-pools-core](https://github.com/0xbow-io/privacy-pools-core) — circuit source and circomlib dependency:

  ```bash
  git clone https://github.com/0xbow-io/privacy-pools-core.git
  cd privacy-pools-core
  npm install
  ```

### Creating wrapper files

The privacy pools circuits use circomkit (no inline `component main`), so you need wrapper `.circom` files:

**commitment_main.circom:**
```circom
pragma circom 2.2.0;
include "commitment.circom";
component main {public [value, label]} = CommitmentHasher();
```

**withdrawal_main.circom:**
```circom
pragma circom 2.2.0;
include "withdraw.circom";
component main {public [withdrawnValue, stateRoot, stateTreeDepth, ASPRoot, ASPTreeDepth, context]} = Withdraw(32);
```

### Compiling

**Important: use `--O1` optimization level.** The default `--O2` aggressively reduces signal count, producing graphs incompatible with the existing zkeys. The `--O1` level matches circom's default optimization used during trusted setup, producing the correct signal counts (commitment: 1542, withdrawal: 36901).

```bash
cd privacy-pools-core/packages/circuits

# Commitment circuit (produces 68 KB graph, 1542 signals)
build-circuit commitment_main.circom commitment.graph.bin \
    -l ../../node_modules -l circuits --O1

# Withdrawal circuit (produces 862 KB graph, 36901 signals)
build-circuit withdrawal_main.circom withdrawal.graph.bin \
    -l ../../node_modules -l circuits --O1
```

### Verifying signal count

The graph signal count must match the zkey. You can verify with `circom` CLI:

```bash
circom commitment_main.circom --r1cs -l ../../node_modules -l circuits
snarkjs r1cs info commitment_main.r1cs
# Should show: # of Wires: 1542
```

If the graph signal count doesn't match the zkey, rapidsnark will error with: `Invalid witness length. Circuit: <expected>, witness: <actual>`.

### Optimization levels

| Flag | Signals (commitment) | Signals (withdrawal) | Compatible with zkey |
|------|---------------------|---------------------|---------------------|
| `--O0` | 2288 | — | No (too many) |
| `--O1` | 1542 | 36901 | Yes |
| `--O2` (default) | 719 | — | No (too few) |

### Placing compiled graphs

Copy the `.graph.bin` files to the embedded resources directory:

```
src/Nethereum.PrivacyPools.Circuits/circuits/
    commitment/commitment.graph.bin
    withdrawal/withdrawal.graph.bin
```

They are automatically embedded by `<EmbeddedResource Include="circuits\**\*" />` in the csproj.

## Package Relationship

| Package | Role |
|---------|------|
| **Nethereum.PrivacyPools** | Core SDK (commitments, tree, accounts, contracts) |
| **Nethereum.PrivacyPools.Circuits** (this) | Embedded circuit artifacts (.wasm, .zkey, .graph.bin, .vk) |
| **Nethereum.ZkProofs.RapidSnark** | Native proof generation (circom-witnesscalc + rapidsnark) |
| **Nethereum.CircomWitnessCalc** | Native witness generation (circom-witnesscalc) |
| **Nethereum.ZkProofs.Snarkjs** | JS proof generation (Node.js / snarkjs) |
| **Nethereum.ZkProofs.Snarkjs.Blazor** | Browser proof generation (Blazor WASM / snarkjs) |

## Credits

- [0xbow-io/privacy-pools-core](https://github.com/0xbow-io/privacy-pools-core) — Privacy Pools circuits
- [iden3/circom-witnesscalc](https://github.com/iden3/circom-witnesscalc) — Native witness calculator
- [iden3/rapidsnark](https://github.com/iden3/rapidsnark) — Native Groth16 prover
- [iden3/circom](https://github.com/iden3/circom) — Circom circuit compiler
