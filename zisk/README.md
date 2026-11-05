# Nethereum EVM → Zisk zkVM

Build tooling and runtime for compiling the Nethereum EVM to a RISC-V 64-bit ELF binary that runs inside the [Zisk](https://github.com/0xPolygonHermez/zisk) zero-knowledge virtual machine. This is the pipeline Nethereum uses to prove Ethereum execution: C# EVM → NativeAOT → RISC-V → Zisk → STARK proof.

## Architecture

```
C# source (Nethereum.EVM.Core, Nethereum.EVM.Zisk, Nethereum.Zisk.Core)
    │
    ▼
bflat-riscv64 (NativeAOT → RISC-V cross-compiler, in Docker)
    │
    └── Links libziskos.a (Zisk 1.3.1-alpha make libziskos)
        ├── zkvm_* accelerators: zkvm_keccak256, zkvm_sha256, zkvm_secp256k1_ecrecover,
        │   zkvm_bn254_*, zkvm_bls12_*, zkvm_kzg_point_eval, zkvm_modexp,
        │   zkvm_secp256r1_verify, zkvm_blake2f
        ├── Raw syscall wrappers: syscall_keccak_f, syscall_sha256_f, syscall_poseidon2,
        │   syscall_secp256k1_*, syscall_bn254_*, syscall_bls12_381_*, syscall_secp256r1_*
        ├── DMA operations: __wrap_memcpy, __wrap_memset, __wrap_memmove, __wrap_memcmp
        └── Guest entry: _start calls main, with an allocator private to the library
    │
    ▼
RISC-V 64-bit ELF (static, no OS, rv64ima without FP)
    │
    ▼
patch_elf (Zisk ELF compatibility post-processing)
    │
    ▼
ziskemu (emulator — fast, no proof)         cargo-zisk prove (STARK proof)
```

`libziskos.a` is Zisk's own isolated staticlib (`make libziskos`), built from the release tag. The guest targets Zisk 1.3.1-alpha.

## How Zisk Precompiles Work

Zisk provides hardware-accelerated cryptographic operations via **CSR (Control Status Register) instructions**. The guest program writes a pointer to a parameter struct into a CSR register; the prover intercepts this, computes the result, and writes it back to the guest's memory.

There are two categories of CSR instructions:

### Regular precompiles (0x800–0x812, 0x817–0x819)

`libziskos.a` also exports `syscall_poseidon1`, `syscall_blake3f`, `syscall_blake2sf` and `syscall_babyjubjub_add`, which the guest does not call.

Single RISC-V instruction: `csrs <CSR>, a0` where `a0` holds the parameter pointer.
The transpiler handles these as atomic operations — no pattern matching on surrounding instructions.

| CSR | Symbol | Operation |
|-----|--------|-----------|
| 0x800 | `syscall_keccak_f` | Keccak-f[1600] permutation (25 × u64 state) |
| 0x801 | `syscall_arith256` | 256-bit arithmetic: dh\|dl = a × b + c |
| 0x802 | `syscall_arith256_mod` | 256-bit modular: d = (a × b + c) mod m |
| 0x803 | `syscall_secp256k1_add` | secp256k1 point addition |
| 0x804 | `syscall_secp256k1_dbl` | secp256k1 point doubling |
| 0x805 | `syscall_sha256_f` | SHA-256 compression (state + 64-byte block) |
| 0x806 | `syscall_bn254_curve_add` | BN254 G1 point addition |
| 0x807 | `syscall_bn254_curve_dbl` | BN254 G1 point doubling |
| 0x808 | `syscall_bn254_complex_add` | BN254 Fp2 addition |
| 0x809 | `syscall_bn254_complex_sub` | BN254 Fp2 subtraction |
| 0x80A | `syscall_bn254_complex_mul` | BN254 Fp2 multiplication |
| 0x80B | `syscall_arith384_mod` | 384-bit modular arithmetic |
| 0x80C | `syscall_bls12_381_curve_add` | BLS12-381 G1 point addition |
| 0x80D | `syscall_bls12_381_curve_dbl` | BLS12-381 G1 point doubling |
| 0x80E | `syscall_bls12_381_complex_add` | BLS12-381 Fp2 addition |
| 0x80F | `syscall_bls12_381_complex_sub` | BLS12-381 Fp2 subtraction |
| 0x810 | `syscall_bls12_381_complex_mul` | BLS12-381 Fp2 multiplication |
| 0x811 | `syscall_add256` | 256-bit addition (returns carry via u64) |
| 0x812 | `syscall_poseidon2` | Poseidon2 Goldilocks permutation (16 × u64 state) |
| 0x817 | `syscall_secp256r1_add` | P-256 point addition |
| 0x818 | `syscall_secp256r1_dbl` | P-256 point doubling |
| 0x819 | `syscall_blake2b_round` | BLAKE2b single round |

### DMA operations (0x813–0x816)

Multi-instruction patterns: `csrs <CSR>, reg` followed by `addi` or `add`. The Zisk transpiler pattern-matches the instruction AFTER the CSR to extract additional parameters (count, fill byte, source/destination). These are NOT general-purpose precompiles.

| CSR | Symbol | Pattern |
|-----|--------|---------|
| 0x813 | `__wrap_memcpy` | `csrs 0x813, src; add x0, dst, count` |
| 0x814 | `__wrap_memcmp` | `csrs 0x814, src; add dst, dst, count` |
| 0x815 | `dma_inputcpy` (Zisk op, not exported) | `csrs 0x815, dst; addi rd, dst, count` |
| 0x816 | `__wrap_memset` | `csrs 0x816, dst; addi x0, count, fill_byte` |

The DMA symbols come from Zisk's `ziskos` crate (renamed to `__wrap_*` by `build-libziskos.sh`).

### Ethereum precompile → CSR mapping

Each Ethereum precompile is orchestrated by a `zkvm_*` function exported by `libziskos.a`, which in turn issues sequences of CSR calls:

| EVM Address | Precompile | Export (`libziskos.a`) | CSR calls used |
|-------------|-----------|-------------------|----------------|
| 0x01 | ECRECOVER | `zkvm_secp256k1_ecrecover` | secp256k1_add/dbl, arith256_mod, keccakf |
| 0x02 | SHA-256 | `zkvm_sha256` | sha256_f |
| 0x03 | RIPEMD-160 | managed C# fallback | (none — pure software) |
| 0x04 | IDENTITY | managed C# | (none) |
| 0x05 | MODEXP | `zkvm_modexp` | arith256_mod |
| 0x06 | BN254 ADD | `zkvm_bn254_g1_add` | bn254_curve_add |
| 0x07 | BN254 MUL | `zkvm_bn254_g1_mul` | bn254_curve_add/dbl |
| 0x08 | BN254 PAIRING | `zkvm_bn254_pairing` | bn254_curve_add/dbl, bn254_complex_add/sub/mul, arith256_mod |
| 0x09 | BLAKE2F | `zkvm_blake2f` | blake2b_round |
| 0x0A | KZG | `zkvm_kzg_point_eval` | bls12_381_* |
| 0x0B–0x12 | BLS12-381 | `zkvm_bls12_g1_add`, `zkvm_bls12_g1_msm`, `zkvm_bls12_g2_add`, `zkvm_bls12_g2_msm`, `zkvm_bls12_pairing`, `zkvm_bls12_map_fp_to_g1`, `zkvm_bls12_map_fp2_to_g2` | bls12_381_curve_add/dbl, bls12_381_complex_add/sub/mul, arith384_mod |
| 0x100 | P256VERIFY | `zkvm_secp256r1_verify` | secp256r1_add/dbl, arith256_mod |

All `zkvm_*` functions are `extern "C"` Rust functions compiled into `libziskos.a`. They handle encoding/decoding, validation, and algorithm orchestration (sponge construction, scalar multiplication, Miller loops, etc.), delegating primitive field/curve operations to CSR instructions.

## Building `libziskos.a` — `zisk/scripts/build-libziskos.sh`

`libziskos.a` contains Zisk's compiled Rust guest runtime: the `zkvm_*` accelerators, the `read_input` / `write_output` IO functions, the DMA memory routines and the guest entry (`_start`, which calls `main`). Zisk builds it itself with `make libziskos`, with fat LTO and an allocator that is private to the library, so none of the earlier source patches are needed.

```bash
bash zisk/scripts/build-libziskos.sh [zisk-git-ref]   # default ref: v1.3.1-alpha
```

The script needs `rustup`, the Zisk Rust toolchain (`cargo-zisk toolchain install`), `git` and `make`. It clones the release tag, runs `make libziskos`, renames the four DMA routines `memcpy`, `memset`, `memmove` and `memcmp` to `__wrap_*` so bflat can link them in place of musl's through the manifest's `wrap_symbols`, and writes `zisk/.libziskos/libziskos.a` and `libziskos.bflat.manifest`:

```json
{
  "name": "libziskos",
  "package_version": "1.0.0",
  "zisk_ref": "v1.3.1-alpha",
  "zisk_commit": "<sha>",
  "source": "Zisk upstream make libziskos",
  "builds": [{
    "arch": "riscv64",
    "os": "linux",
    "libc": "zisk",
    "static_lib": "runtimes/linux-riscv64/native/libziskos.a"
  }],
  "wrap_symbols": ["memcpy", "memset", "memmove", "memcmp"]
}
```

The build image (`ci/Dockerfile.zisk`) runs this script and ships the result in `/opt/libziskos`.

## Nethereum Native Bindings — `Nethereum.Zisk.Core.ZiskCrypto`

Each accelerator the guest uses is exposed to C# as a `[DllImport("__Internal")]` method. The `__Internal` library name tells NativeAOT to resolve the symbol statically from whatever `.a` files were linked — no runtime dynamic loading. The signatures follow the zkvm-standards C header (`zkvm_accelerators.h`): each returns a `zkvm_status` (`0` is success) and the checks return their result through a `bool*`.

```csharp
public static class ZiskCrypto
{
    public static extern uint zkvm_keccak256(byte[] data, nuint len, byte[] output);
    public static extern uint zkvm_sha256(byte[] data, nuint len, byte[] output);
    public static extern uint zkvm_ripemd160(byte[] data, nuint len, byte[] output);
    public static extern uint zkvm_secp256k1_ecrecover(byte[] msg, byte[] sig, byte recid, byte[] output);
    public static extern uint zkvm_modexp(byte[] base_ptr, nuint base_len, byte[] exp_ptr, nuint exp_len, byte[] modulus_ptr, nuint mod_len, byte[] output);
    public static extern uint zkvm_bn254_g1_add(byte[] p1, byte[] p2, byte[] result);
    public static extern uint zkvm_bn254_g1_mul(byte[] point, byte[] scalar, byte[] result);
    public static extern unsafe uint zkvm_bn254_pairing(byte[] pairs, nuint num_pairs, bool* verified);
    public static extern unsafe uint zkvm_bls12_pairing(byte[] pairs, nuint num_pairs, bool* verified);
    public static extern uint zkvm_blake2f(uint rounds, ulong[] h, ulong[] m, ulong[] t, byte f);
    public static extern unsafe uint zkvm_kzg_point_eval(byte[] commitment, byte[] z, byte[] y, byte[] proof, bool* verified);
    public static extern uint zkvm_bls12_g1_add(byte[] p1, byte[] p2, byte[] result);
    public static extern uint zkvm_bls12_g1_msm(byte[] pairs, nuint num_pairs, byte[] result);
    public static extern uint zkvm_bls12_g2_add(byte[] p1, byte[] p2, byte[] result);
    public static extern uint zkvm_bls12_g2_msm(byte[] pairs, nuint num_pairs, byte[] result);
    public static extern uint zkvm_bls12_map_fp_to_g1(byte[] fp, byte[] result);
    public static extern uint zkvm_bls12_map_fp2_to_g2(byte[] fp2, byte[] result);
    public static extern unsafe uint zkvm_secp256r1_verify(byte[] msg, byte[] sig, byte[] pk, bool* verified);
    public static extern unsafe void syscall_poseidon2(ulong* state);
}
```

(Each declaration carries `[DllImport("__Internal")]` in the source.) At runtime each call goes C# → NativeAOT P/Invoke thunk → statically-linked symbol in `libziskos.a` → CSR instruction(s) → prover intercept. `syscall_poseidon2` is the raw Goldilocks permutation that Zisk exports directly.

## Hash provider implementations (C#)

For the EIP-7864 binary state trie, we need `IHashProvider` implementations that work inside the zkVM guest:

| Provider | Rust function called | Sponge / schedule handling |
|----------|----------------------|----------------------------|
| `ZiskKeccakHashProvider` | `zkvm_keccak256` → CSR 0x800 | Rust handles padding + sponge |
| `ZiskSha256HashProvider` | `zkvm_sha256` → CSR 0x805 | Rust handles schedule + padding |
| `ZiskPoseidonHashProvider` | `syscall_poseidon2` → CSR 0x812 | C# sponge: width 16, rate 12, digest 4 × u64 |

Keccak and SHA-256 delegate entirely to the Rust `zkvm_*` functions which handle padding, sponge / message-schedule, and CSR calls. Poseidon2 is different: Zisk only exports the raw permutation, so `ZiskPoseidonHashProvider` does the Goldilocks sponge construction itself in C# (absorb up to 12 × u64 from input into state, call `syscall_poseidon2(state)` to permute, squeeze 4 × u64 from state for the digest).

`ZiskBinaryWitness.cs` routes the hash function at witness-read time:

```
Witness features.HashFunction →
    Blake3    → Blake3HashProvider (managed, no CSR)
    Poseidon  → ZiskPoseidonHashProvider (C# sponge + CSR 0x812)
    Sha256    → ZiskSha256HashProvider  (CSR 0x805 via zkvm_sha256)
    Keccak    → ZiskKeccakHashProvider  (CSR 0x800 via zkvm_keccak256)
```

## Building the EVM ELF — `zisk/scripts/build.sh`

```bash
bash zisk/scripts/build.sh
```

The script compiles inside the bflat toolchain. On a host with Docker it starts the `nethereum/bflat-riscv64:zisk-1.3.1` image (`ZISK_IMAGE` overrides it, `ZISK_CPUS` limits it, default 8); inside the `nethereum-zisk` image (`ci/Dockerfile.zisk`, repository mounted at `/src`) it runs the toolchain directly. It reads `libziskos.a` from `zisk/.libziskos` (`LIBZISKOS_DIR` overrides it).

Two modes:

| Mode | Command | Trade-off |
|------|---------|-----------|
| `--source` (default) | `bash zisk/scripts/build.sh` | bflat compiles all `.cs` sources in one invocation — smaller ELF, cross-assembly inlining works. Production builds. |
| `--dll` | `bash zisk/scripts/build.sh --dll` | `dotnet build` produces a DLL first, bflat links it — faster iteration, ~25% larger ELF (no cross-assembly inlining). |

### What source-mode compiles

The source list is the `Compile` items of `src/Nethereum.EVM.Zisk/Nethereum.EVM.Zisk.csproj` (`dotnet msbuild -getItem:Compile`), so the project file is the single list of what the guest contains: `Nethereum.Zisk.Core`, the `Nethereum.EVM.Zisk` bridge, the minimal `Nethereum.Util` and `Nethereum.Hex` files, `Nethereum.EVM.Core`, `Nethereum.Merkle.Patricia` (without proof verification), `Nethereum.Merkle.Binary`, the four state-root calculators of `Nethereum.CoreChain`, the selected `Nethereum.Model` files, `Nethereum.RLP`, and the BLS12-381 and KZG precompile handlers. After changing `Nethereum.EVM.Core`, run `dotnet build src/Nethereum.EVM.Zisk`: it is the only build that sees a dependency the guest does not link.

### Step 1 — `bflat build`

```
bflat build $SRC \
    --os linux --arch riscv64 --libc zisk \
    --no-globalization --no-pthread --no-stacktrace-data \
    --no-exception-messages \
    -Os --no-pie \
    -d EVM_SYNC -d EVM_ZISK \
    --extlib <libziskos>/libziskos.bflat.manifest \
    -o zisk/output/nethereum_evm_raw
```

- `--libc zisk` — use Zisk's libc shims (provided by the bflat image); the fork's linker script lays the image out for Zisk 1.3.1 (ROM at `0x80000000`, RAM from `0xA0430000`, stack top `0xA0400000`)
- `--no-globalization --no-pthread --no-stacktrace-data --no-exception-messages` — drop runtime features that pull in too much code
- `-Os --no-pie` — optimise for size, position-dependent (zkVM is static)
- `-d EVM_SYNC` — enable the sync EVM engine (the async one pulls in `System.Threading.Tasks` which is huge)
- `-d EVM_ZISK` — build for the guest: no `ConditionalWeakTable` code-analysis cache, whose dependent handles return null under the zero-GC runtime
- `--extlib` — the libziskos manifest; bflat reads it to locate the static lib AND the `wrap_symbols` array
- the fork links with `--wrap=main`: Zisk's `_start` calls `main`, which becomes the bflat bootstrap (`__libc_start_main`)

### Step 2 — `patch_elf`

bflat runs `patch_elf` itself after linking and writes `nethereum_evm_raw.patched`, which the script renames to `nethereum_evm_elf`:

```
patch_elf nethereum_evm_raw nethereum_evm_raw.patched \
    --fix-init-array --fix-tdata --remove-eh --trim-bss
```

- `--fix-init-array` — rewrites `.init_array` so .NET static constructors run in correct order
- `--fix-tdata` — normalises thread-local data (Zisk is single-threaded but TLS section layout must be sane)
- `--remove-eh` — strips `.eh_frame` and `.dotnet_eh` (no exception unwinding in Zisk)
- `--trim-bss` — removes the `.bss` section header

## The `nethereum-zisk` image — `ci/Dockerfile.zisk`

One image carries everything the Zisk lane needs: the .NET SDK, the bflat toolchain, `libziskos.a`, `ziskemu`, `cargo-zisk` (the GPU build, which also proves on the CPU when `--gpu` is left out), `snarkjs` 0.7.6 (needed by `cargo-zisk verify` for a PLONK proof) and the runtime libraries they load.

```bash
docker build --cpu-period 100000 --cpu-quota 800000 -f ci/Dockerfile.zisk -t nethereum-zisk zisk/scripts
```

It needs the `nethereum/bflat-riscv64:zisk-1.3.1` image first (`ARG BFLAT_IMAGE`). The proving key is not in the image: it is 17 GB and grows to about 72 GB once its constant trees are generated. Install it on the host with `ziskup --provingkey` and mount it:

```bash
docker run --rm --gpus all -v ~/.zisk/provingKey:/opt/zisk/provingKey nethereum-zisk \
    cargo-zisk prove --gpu -e zisk/output/nethereum_evm_elf -i witness_standard.bin -o proof
```

`--gpus all` needs the NVIDIA container toolkit on the host. Proving on the GPU needs more video memory than a 12 GB or 16 GB card has (see Proving notes).

## Prerequisites

- **Docker**
- **A proving key** from `ziskup --provingkey` — only needed to prove, not to build or emulate

## Quick Start

### 1. Build the images

```bash
git clone --branch zisk-1.3.1 https://github.com/Nethereum/bflat-riscv64.git ../bflat-riscv64
cd ../bflat-riscv64 && docker build -t nethereum/bflat-riscv64:zisk-1.3.1 .
cd - && docker build -f ci/Dockerfile.zisk -t nethereum-zisk zisk/scripts
```

### 2. Build libziskos.a from Zisk (only outside the image)

`nethereum-zisk` already carries `libziskos.a` in `/opt/libziskos`. To build it on a host with the Zisk toolchain: `bash zisk/scripts/build-libziskos.sh` writes `zisk/.libziskos/`.

### 3. Compile the EVM to a Zisk-ready ELF

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-zisk bash zisk/scripts/build.sh
```

Output: `zisk/output/nethereum_evm_elf` (~5.8 MB).

### 4. Generate a witness

```bash
dotnet test tests/Nethereum.EVM.Core.Tests --filter "GenerateSimpleSstoreWitness"
```

Produces `zisk/output/test_sstore.bin` — a serialised `BinaryBlockWitness` with a block, one SSTORE transaction, pre-state accounts, and the signed transaction RLP.

### 5. Run in the emulator

```bash
ziskemu -e zisk/output/nethereum_evm_elf --legacy-inputs zisk/output/test_sstore.bin -n 500000000
```

Expected output ends with the guest's block roots and gas:

```
BIN:reading
BIN:block txs=1 accounts=2
BIN:exec
BIN:executed ok=1 fail=0
BIN:state_root=0x...
BIN:OK gas=43106
```

### 6. Run the Zisk test lane

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-zisk ci/run-tests.sh Zisk
```

## Environment overrides

- `ZISK_IMAGE=<name>` — use a different bflat Docker image
- `ZISK_CPUS=<n>` — CPUs given to the build container (default 8)
- `LIBZISKOS_DIR=<path>` — directory containing `libziskos.bflat.manifest` (default `zisk/.libziskos/`, `/opt/libziskos` in the image)

## Common pitfalls when upgrading

### `LIBZISKOS_DIR` must point at a libziskos that exports every `ZiskCrypto.cs` symbol

`ZiskCrypto.cs` binds symbols that `libziskos.a` exports. When a binding is added or the Zisk tag changes, rebuild `libziskos.a` (`bash zisk/scripts/build-libziskos.sh <ref>`), otherwise the link fails with `lld: error: undefined symbol: ...`.

### Never call the BCL from the guest

The guest has no OpenSSL and no OS. Code in `Nethereum.EVM.Core` that reaches `System.Security.Cryptography` links, then jumps to a null function pointer when it runs (`CryptoNative_EvpSha256` in the trace). Hash through the hardfork's precompile backends, as `ExecutionRequests` does for the EIP-7685 requests hash.

### The test runners read the ELF from `ExternalPaths:ZiskOutput`

`tests/Nethereum.XUnitEthereumClients/appsettings.test.json` sets `ExternalPaths:ZiskOutput` to `zisk/output`, which is where `build.sh` writes. The `ZiskEmu_*` tests do not run against an old ELF unless you copy one there.

### The bflat source set is the csproj

`build.sh` compiles the `Compile` items of `Nethereum.EVM.Zisk.csproj`. A file outside that project does not reach the guest; add it to the project and `dotnet build src/Nethereum.EVM.Zisk` shows what else it needs.

### Headless hosts

Set `HWLOC_COMPONENTS=-gl` (the image does) so OpenMPI does not probe X11.

## Directory layout

```
zisk/
├── README.md                 # This file
├── .libziskos/               # Built by build-libziskos.sh (gitignored)
│   ├── libziskos.a           # Zisk's isolated staticlib with the DMA routines renamed to __wrap_*
│   ├── libziskos.bflat.manifest
│   └── runtimes/linux-riscv64/native/libziskos.a
├── scripts/
│   ├── build.sh              # EVM → ELF build (source + DLL modes)
│   ├── build-libziskos.sh    # Build libziskos.a from the Zisk release tag
│   ├── analyze-elf.sh        # Diagnostic: dump text symbol sizes
│   └── analyze-sections.sh   # Diagnostic: section sizes + flags
├── test-artifacts/           # Guest ELF, witnesses and STARK proofs regenerated with Zisk 1.3.1-alpha
└── output/                   # ELF + witness artefacts (gitignored)
```

## Validated test results

Each row is the guest's output for the SSTORE block of `BinaryTrieWitnessExecutionTests`, compared with the host execution of the same witness by the `*_GuestOutputsMatchHost` tests:

| State tree | State root | Block hash |
|---------|-----------|-----------|
| Patricia + Keccak | `0x1461e627a8cb9074cb9a7c0ed38d1c49af19aaf31b9b485eaa74444660e7dc88` | `0x85230f180e0e64f42d0172e2dce096efd8ab58346f9e3d49ae0091514b4a1797` |
| Binary + Blake3 | `0x5f9b415dd4032fcac485453daf4e2ccd415d3f20574094c58329fe6b66b4aeb8` | `0x3c9324c269bc1f78b6d42e4772f7ec2d92d417982efc6f71fdb443d9ccaf57d9` |
| Binary + Poseidon2 | `0x98f27cabfd72400aa9c064c68fb66e00de59ca0e3b586bc63b0ff5ac05712070` | `0x3421f169cc0cd1b7f637dc4972828d0ead8ea1b76fe485852c2a3b54dcb8aad9` |

All three report `BIN:OK gas=43106`. The Patricia and Poseidon values are the ones pinned by `Patricia_AllOutputsMatchZisk` and `BinaryPoseidon_AllOutputsMatchZisk`. A block at or after Prague must carry the EIP-7002, EIP-7251 and builder request predeploys, otherwise the guest stops at the system call that finds no code; the earlier roots did not include them.

The `Category=Zisk` lane (EVM.Core.Tests, EVM.UnitTests, CoreChain.IntegrationTests) passes except `ZiskEmu_Prague_Eip2537_Bls12_381`, whose 24 `invalid` cases stop the guest (a precompile rejects bad input by throwing and the guest cannot catch).

## What the `Nethereum/bflat-riscv64` fork adds

The `zisk-1.3.1` branch of [Nethereum/bflat-riscv64](https://github.com/Nethereum/bflat-riscv64/tree/zisk-1.3.1) carries patches on top of the upstream bflat compiler:

| Patch | File | Why |
|-------|------|-----|
| Zisk 1.3.1 memory map | `src/bflat/modules/zkvm_zisk/script.ld` | ROM 128 MB at `0x80000000`, RAM from `0xA0430000` (after the 4 MB stack, system registers and output area), `_init_stack_top` at `0xA0400000`, and `.init_array` and `.fini_array` in ROM with the `__init_array_*` and `__fini_array_*` bounds that the ziskos entry reads. |
| Entry through `main` | `src/bflat/modules/zkvm_zisk/module.S`, `src/bflat/BuildCommand.cs` | Zisk's `_start` calls `main`; the fork links `--wrap=main` so `__wrap_main` starts the bflat runtime. |
| TSS reduction | `src/bflat/modules/rhp/module.c` — `TSS_MAX_TYPEMANAGERS 1024→32`, `TSS_MAX_SLOTS 4096→256` | Default values bloat `.bss` to about 33.5 MB. Reduced to 64 KB. |
| `calloc` → `malloc` + `memset` | same file — all `__wrap_Rhp*` allocators | `pal.o` wraps `malloc` via `--wrap` but not `calloc`; the default `calloc` path produces unresolved `__real_calloc` in a Zisk build. |

## Related source projects

| Project | Role |
|---------|------|
| `src/Nethereum.EVM.Core` | Portable sync-capable EVM engine (no platform deps). Source-shared into Nethereum.EVM and the Zisk guest. |
| `src/Nethereum.EVM.Zisk` | Zisk guest bridge: `ZiskBinaryWitness` entry point, precompile backends, state-root resolution. |
| `src/Nethereum.Zisk.Core` | Low-level Zisk runtime: `ZiskCrypto` DllImports, `ZiskIO`, `ZiskInput`, `ZiskPoseidonHashProvider`, `ZiskKeccakHashProvider`, `ZiskSha256HashProvider`. |
| `src/Nethereum.Merkle.Binary` | EIP-7864 binary state trie: `BinaryTrie`, `BinaryStateRootCalculator`, pluggable `IHashProvider`. |

## Verification

After a successful build:

```bash
# 1. EH sections removed
riscv64-linux-gnu-readelf -S zisk/output/nethereum_evm_elf | grep -E "eh_frame|dotnet_eh"

# 2. No System.Runtime.Numerics (BigInteger library not pulled in)
riscv64-linux-gnu-nm zisk/output/nethereum_evm_elf | grep System_Runtime_Numerics

# 3. Emulator runs
ziskemu -e zisk/output/nethereum_evm_elf --legacy-inputs zisk/output/test_sstore.bin -n 500000000
```

## Proving notes

### Environment — CRITICAL

OpenMPI's hwloc component tries to probe GPU topology via X11. On headless servers (SSH, CI) this causes `Authorization required, but no authorization protocol specified` and hangs the process. **Always set:**

```bash
export HWLOC_COMPONENTS=-gl
```

Add this to `~/.bashrc` or prefix every `cargo-zisk` command with it.

### Input format — CRITICAL

Three formats exist. Using the wrong one causes `BIN:bad version` errors.

| Format | Header | Used by |
|---|---|---|
| **Native** | `[u8 version][witness data...]` | `BinaryBlockWitness.Serialize()` output |
| **Legacy** | `[u64 zero][u64 len][native...]` (16-byte header) | `ziskemu --legacy-inputs` |
| **Standard** | `[u64 legacy_size][u64 zero][u64 len][native...]` (24-byte header) | `cargo-zisk prove -i` |

**`cargo-zisk prove -i` REQUIRES standard format.** Native or legacy files will fail with `BIN:bad version`.

Convert native → standard:
```python
import struct
with open('witness.bin', 'rb') as f: data = f.read()
header = struct.pack('<QQQ', 16 + len(data), 0, len(data))
with open('witness_standard.bin', 'wb') as f: f.write(header + data)
```

Or in C#: `ZiskBinaryWriter.ToStandardInputFormat()` wraps native bytes in standard format.

Auto-detection in the RISC-V binary (`ZiskInput.Read()`): reads the u64 at offset 16 — if zero → legacy, if non-zero → standard. This works when the tool passes data correctly, but **the tool must frame it properly**.

## SNARK (PLONK) proof and Solidity verification

A STARK proof (VADCOP final) is wrapped into a PLONK proof that a Solidity contract verifies.

```bash
ziskup setup_snark                                  # PLONK proving key into ~/.zisk/provingKeySnark
bash zisk/scripts/wrap-plonk.sh proof proof_plonk   # cargo-zisk wrap --plonk in the nethereum-zisk image, then cargo-zisk verify
cargo-zisk-dev export-solidity-calldata -p proof_plonk -o fixture.json
```

`export-solidity-calldata` writes the four arguments of `verifySnarkProof`: `programVK`, `rootCVadcopFinal`, `publicValues` and `proofBytes` (24 uint256 words). `cargo-zisk verify -p proof_plonk` checks the PLONK proof off-chain; it needs `snarkjs` on the `PATH` and stops with `Failed to execute snarkjs` without it.

**Measured 2026-10-10, STARK proof of `witness_eth_transfer_noroot` (4,877,459 steps with the ELF of that date; the committed ELF takes 2,898,761, see `test-artifacts/README.md`) on 10 CPUs, wrap on 12 CPUs:**

| | |
|---|---|
| `ziskup setup_snark` | 21.9 GB download, 25 GB on disk, in addition to the 72 GB STARK key |
| `cargo-zisk wrap --plonk` | 6 m 43 s for the generic proof, 10 m 26 s for the anchor proof |
| Memory | `MemAvailable` fell from 118 GB to 31 GB during the wrap (about 87 GB) |
| Proof file | 2,912 bytes; `proofBytes` is 768 bytes, `publicValues` 512 bytes |
| `cargo-zisk verify` | `PLONK proof was verified` |
| `verifySnarkProof` on the DevChain | `eth_estimateGas` 340,871 |

The wrap needs the `final.so` of the SNARK key to be loaded with an executable stack; run it in the `nethereum-zisk` image (Ubuntu 24.04). On the Ubuntu host with a newer glibc the run stopped with `cannot enable executable stack as shared object requires`.

The verifier contract is `src/Nethereum.AppChain.Contracts/src/ZiskVerifier.sol`, generated for the 1.3.1-alpha PLONK key (`VERSION()` returns `v1.3.1-alpha`). `Given_TheRealPlonkProof_When_VerifySnarkProofIsCalled_Then_ItDoesNotRevert` in `tests/Nethereum.AppChain.Anchoring.IntegrationTests/ZiskVerifierTests.cs` deploys it to the in-process DevChain and calls it with the committed fixture `Fixtures/zisk_plonk_fixture.json`; a changed proof word, a changed public value, another `programVK` and another `rootCVadcopFinal` each revert with `InvalidProof`. Those tests need no prover (`Category=ZiskVerifier`). `ZiskProofToolingTests` (`Category=Zisk`) re-verifies the committed STARK and PLONK proofs with `cargo-zisk` and re-exports the fixture from `zisk/test-artifacts/proofs/plonk_eth_transfer_noroot`.

### Two proof calls from one guest

| | Generic proof | Anchor proof |
|---|---|---|
| Witness | `BinaryBlockWitness` as before | the same witness with bit `0x01` of the second flags byte set and `AnchorInputs` appended after the accounts |
| Public values | slots 0-55 as listed in `zisk/test-artifacts/README.md` | slots 0-7 hold `keccak256(AnchorInputs)`, slots 8-63 are the generic slots shifted by 8 |
| Verified by | `ZiskVerifier.verifySnarkProof` | `ZiskVerifierAdapter.verify(proof, anchorInputs)` (and `ZiskVerifier.verifySnarkProof`) |

A witness without `AnchorInputs` serialises to the same bytes as before and the guest writes the same output registers: for four generic witnesses the output files of the earlier and the current ELF were compared byte for byte and are identical.

`AnchorInputs` is the byte string the anchor contract hashes: `ZiskVerifierAdapter.verify` computes `keccak256(abi.encodePacked(publicInputs))` over the `uint256[]` it is given (`src/Nethereum.AppChain.Contracts/src/ZiskVerifierAdapter.sol`, `inputsCommitment`), that is each word as 32 big-endian bytes, concatenated. `AnchorBinding.Encode(IReadOnlyList<EvmUInt256>)` builds it and `AnchorBinding.Commitment` hashes it. For `AppChainAnchor` the words are the 11 values of `_buildBatchPublicInputs`. The guest hashes whatever bytes it is given; it does not check them against the block it executed.

Each Zisk public is a 64-bit value that holds one 32-bit output register, so 32 bytes of commitment take eight publics and the adapter rebuilds the hash from the first four bytes of each of the first eight 8-byte groups of `publicValues` (the last four bytes of each group must be zero). The guest writes the hash with the same big-endian word order it uses for every other hash output.

To prove for an anchor from C#, set `BlockWitnessData.AnchorInputs` before `BinaryBlockWitness.Serialize`, or call `ZiskProveBlockProver.ProveBlockForAnchorAsync(witnessBytes, anchorPublicInputs, preStateRoot, postStateRoot, blockNumber)`, which attaches the inputs to an existing witness. `ZiskProveBlockProver.ProveBlockAsync` is unchanged.

The first wrap with a freshly installed SNARK key writes a constant tree into `provingKeySnark`, so that directory must be mounted writable (`wrap-plonk.sh` does); with a read-only mount the wrap stops while loading the recursive setup, on a failed file write.

A production integration holds `programVK` and `rootCVadcopFinal` as constants of its own contract; `export-solidity-calldata` reads them from the proof only for fixtures.

## Proving end-to-end

Zisk 1.3.1-alpha proves and verifies the ELF exactly as `build.sh` writes it: stock Zisk release binaries, no fork, no security-cookie patch and no `ziskfloat` rebuild.

```bash
export HWLOC_COMPONENTS=-gl

# 1. Execute only
cargo-zisk execute -e zisk/output/nethereum_evm_elf -i witness_standard.bin

# 2. STARK proof, verified after generation
cargo-zisk prove -e zisk/output/nethereum_evm_elf -i witness_standard.bin -o proof -y

# 3. Verify later
cargo-zisk verify -p proof

# 4. PLONK wrap (needs the SNARK key from `ziskup setup_snark`, see SNARK and Solidity verification)
cargo-zisk wrap -p proof --plonk -o proof_plonk
```

`cargo-zisk execute` and `prove` run the program on several threads and the guest's `BIN:` lines arrive interleaved; read the roots from `ziskemu` or from the proof's public outputs.

**Measured 2026-10-09 on a 24-thread Linux host, `cargo-zisk prove -m` pinned to 8 cores, CPU proving:** the CREATE2 call block (`CREATE2_CallData_0_0_0`, 5,083,581 steps) proved and verified in 13 m 55 s; the first prove after installing the key also generates the constant trees (about 10 minutes, 55 GB on disk).

**GPU memory:** `cargo-zisk prove --gpu` refuses to start with `Not enough GPU memory to run the proof` on an RTX 5060 Ti (15.3 GB free) and on an RTX 3060 (11.5 GB free). The prover reports a maximum prover buffer of 19.49 GB plus a 1.5 GB reserve, so it needs about 21 GB free on every GPU it uses; `-m` does not change that. With several GPUs it takes the smallest one.
