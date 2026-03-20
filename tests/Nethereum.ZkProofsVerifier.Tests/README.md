# Nethereum.ZkProofsVerifier Tests

## Overview

This test suite validates the Groth16 ZK proof verifier against real Circom/snarkjs artifacts and cross-validates against the Solidity verifier running in the Nethereum EVM.

## Test Categories

| Category | Tests | Purpose |
|---|---|---|
| `Groth16VerifierTests` | 5 | Input validation, null checks, mismatched public inputs |
| `SnarkjsParserTests` | 7 | JSON parsing correctness for proof, VK, and public inputs |
| `CircomIntegrationTests` | 5 | End-to-end verification with the multiplier circuit |
| `ExternalVectorTests` | 8 | Independent circuits (square, threeinputs) with separate trusted setups |
| `EvmCrossValidationTests` | 2 | Native verifier vs Solidity verifier running in EVM |

## Test Circuits

### 1. Multiplier (`TestData/`)

Circuit: `a * b = c` with public inputs `[a, b]` and public output `[c]`.

Test case: `a=3, b=11 → c=33`, public signals: `["33", "3", "11"]`

### 2. Square (`TestData/square/`)

Circuit: `x * x = y` with public input `[x]` and public output `[y]`.

Test case: `x=7 → y=49`, public signals: `["49", "7"]`

### 3. ThreeInputs (`TestData/threeinputs/`)

Circuit: `a * b + c = result` with public inputs `[a, b, c]` and public output `[result]`.

Test case: `a=5, b=13, c=42 → result=107`, public signals: `["107", "5", "13", "42"]`

## Running Tests

```bash
dotnet test tests/Nethereum.ZkProofsVerifier.Tests/
```

Filter by category:
```bash
dotnet test --filter "Category=ZK-Integration"
dotnet test --filter "Category=ZK-ExternalVector"
dotnet test --filter "Category=ZK-EvmCrossValidation"
```

## How Test Vectors Were Generated

### Prerequisites

- [Circom](https://github.com/iden3/circom) compiler (requires Rust)
- [snarkjs](https://github.com/iden3/snarkjs) (Node.js)
- [solcjs](https://github.com/nicksavers/solcjs) (for EVM cross-validation)

```bash
# Install circom from source
git clone https://github.com/iden3/circom.git
cd circom && cargo build --release
cp target/release/circom ~/.cargo/bin/

# Install snarkjs and solcjs
npm install -g snarkjs solc
```

### Step-by-step: Generate a New Test Vector

#### 1. Write a Circom circuit

```circom
// circuit.circom
pragma circom 2.0.0;

template Multiplier() {
    signal input a;
    signal input b;
    signal output c;
    c <== a * b;
}

// public [a, b] means a and b are public inputs
// c is a public output by default
component main {public [a, b]} = Multiplier();
```

#### 2. Compile the circuit

```bash
circom circuit.circom --r1cs --wasm --sym -o .
```

This produces:
- `circuit.r1cs` — rank-1 constraint system
- `circuit_js/circuit.wasm` — witness calculator
- `circuit.sym` — debug symbols

#### 3. Generate the trusted setup (Powers of Tau + circuit-specific)

```bash
# Phase 1: Powers of Tau ceremony (universal, reusable)
snarkjs powersoftau new bn128 12 pot12_0000.ptau
snarkjs powersoftau contribute pot12_0000.ptau pot12_0001.ptau --name="contributor" -e="random entropy"
snarkjs powersoftau prepare phase2 pot12_0001.ptau pot12_final.ptau

# Phase 2: Circuit-specific setup
snarkjs groth16 setup circuit.r1cs pot12_final.ptau circuit_final.zkey

# Export the verification key
snarkjs zkey export verificationkey circuit_final.zkey verification_key.json
```

The `pot12` ceremony supports circuits with up to 2^12 = 4096 constraints. Use a larger power for bigger circuits.

#### 4. Generate a proof

```bash
# Create witness input
echo '{"a": "3", "b": "11"}' > input.json

# Generate proof and public signals
snarkjs groth16 fullprove input.json circuit_js/circuit.wasm circuit_final.zkey proof.json public.json
```

#### 5. Verify with snarkjs (reference check)

```bash
snarkjs groth16 verify verification_key.json public.json proof.json
# Should output: [INFO]  snarkJS: OK!
```

#### 6. Copy test artifacts

Only three files are needed for the .NET test:
- `proof.json` — the Groth16 proof (G1/G2 points)
- `verification_key.json` — the verification key (alpha, beta, gamma, delta, IC points)
- `public.json` — public signals array

```bash
cp proof.json verification_key.json public.json tests/Nethereum.ZkProofsVerifier.Tests/TestData/your_circuit/
```

### Step-by-step: EVM Cross-Validation Setup

The EVM cross-validation test compares our native Groth16Verifier with a snarkjs-generated Solidity verifier contract running in the Nethereum EVM simulator.

#### 1. Export the Solidity verifier

```bash
snarkjs zkey export solidityverifier circuit_final.zkey verifier.sol
```

This generates a Solidity contract with the verification key constants baked in and a `verifyProof` function that uses the BN128 precompiles (ecAdd at 0x06, ecMul at 0x07, ecPairing at 0x08).

#### 2. Compile to EVM bytecode

```bash
solcjs --bin verifier.sol
```

This produces `verifier_sol_Groth16Verifier.bin` — the creation (init) bytecode in hex.

#### 3. How the test works

The `EvmCrossValidationTests` class:

1. **Deploys** the contract: runs the init bytecode through the EVM, captures the returned runtime bytecode
2. **Encodes the call**: ABI-encodes `verifyProof(uint256[2] _pA, uint256[2][2] _pB, uint256[2] _pC, uint256[N] _pubSignals)` with the proof data
3. **Executes**: runs the runtime bytecode with the encoded calldata in the EVM simulator
4. **Compares**: checks that both native and EVM verifiers return the same result

The Solidity verifier uses inline assembly that directly calls the BN128 precompiles, making this a true end-to-end validation of the entire verification pipeline.

#### G2 Point Encoding (The Critical Detail)

snarkjs `proof.json` stores G2 points as:
```json
"pi_b": [
  ["x_c0_real", "x_c1_imaginary"],
  ["y_c0_real", "y_c1_imaginary"],
  ["1", "0"]
]
```

The Ethereum BN128 pairing precompile (EIP-197) expects G2 coordinates as `[imaginary, real]`.

snarkjs encodes Solidity calldata with G2 as `[c1, c0]` = `[imaginary, real]` per coordinate.

Our `SnarkjsProofParser` maps `[c0, c1]` → `new Fp2(c1, c0)` where `Fp2(a, b)` means `a*i + b` (a=imaginary, b=real).

Both paths produce the same pairing result, confirmed by the cross-validation tests.

## Test Fixture File Formats

### proof.json

```json
{
  "pi_a": ["x_decimal", "y_decimal", "1"],
  "pi_b": [
    ["x_c0_decimal", "x_c1_decimal"],
    ["y_c0_decimal", "y_c1_decimal"],
    ["1", "0"]
  ],
  "pi_c": ["x_decimal", "y_decimal", "1"],
  "protocol": "groth16",
  "curve": "bn128"
}
```

- `pi_a`, `pi_c`: G1 points in projective coordinates (third element is always "1" for affine)
- `pi_b`: G2 point where each coordinate is an Fp2 element `[c0_real, c1_imaginary]`

### verification_key.json

```json
{
  "protocol": "groth16",
  "curve": "bn128",
  "nPublic": 3,
  "vk_alpha_1": ["x", "y", "1"],
  "vk_beta_2": [["x_c0", "x_c1"], ["y_c0", "y_c1"], ["1", "0"]],
  "vk_gamma_2": [["x_c0", "x_c1"], ["y_c0", "y_c1"], ["1", "0"]],
  "vk_delta_2": [["x_c0", "x_c1"], ["y_c0", "y_c1"], ["1", "0"]],
  "IC": [
    ["x", "y", "1"],
    ["x", "y", "1"],
    ...
  ]
}
```

- `IC` has `nPublic + 1` elements: IC[0] is the constant term, IC[1..n] correspond to public signals
- `nPublic` counts both public inputs and public outputs

### public.json

```json
["output_0", "output_1", ..., "input_0", "input_1", ...]
```

Public outputs come first, then public inputs, matching the order in the circuit's constraint system.

## Verification Equation

The Groth16 verification checks:

```
e(A, B) = e(alpha, beta) * e(vk_x, gamma) * e(C, delta)
```

Where `vk_x = IC[0] + sum(publicInput[i] * IC[i+1])`.

Rearranged for `BN128Pairing.PairingCheck` (product of pairings equals 1):

```
e(-A, B) * e(alpha, beta) * e(vk_x, gamma) * e(C, delta) = 1
```

This is a single call to `PairingCheck` with 4 G1/G2 point pairs.
