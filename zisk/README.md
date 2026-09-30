# Nethereum EVM → Zisk zkVM

Build tooling and runtime for compiling the Nethereum EVM to a RISC-V 64-bit ELF binary that runs inside the [Zisk](https://github.com/0xPolygonHermez/zisk) zero-knowledge virtual machine. This is the pipeline Nethereum uses to prove Ethereum execution: C# EVM → NativeAOT → RISC-V → Zisk → STARK proof.

## Architecture

```
C# source (Nethereum.EVM.Core, Nethereum.EVM.Zisk, Nethereum.Zisk.Core)
    │
    ▼
bflat-riscv64 (NativeAOT → RISC-V cross-compiler, in Docker)
    │
    └── Links libziskos.a (our build from Zisk upstream + poseidon2_c trampoline)
        ├── Rust `_c` functions: keccak256_c, sha256_c, secp256k1_ecdsa_address_recover_c,
        │   bn254_pairing_check_c, bls12_381_*, verify_kzg_proof_c, modexp_bytes_c,
        │   secp256r1_ecdsa_verify_c, blake2b_compress_c
        ├── Raw syscall wrappers: syscall_keccak_f, syscall_sha256_f, syscall_poseidon2,
        │   syscall_secp256k1_*, syscall_bn254_*, syscall_bls12_381_*, syscall_secp256r1_*
        ├── DMA operations: __wrap_memcpy, __wrap_memset, __wrap_memmove, __wrap_memcmp
        └── Our trampoline: poseidon2_c (1-line C alias baked in by build-libziskos.sh)
    │
    ▼
RISC-V 64-bit ELF (static, no OS, rv64ima without FP)
    │
    ▼
patch_elf + signal_patch (Zisk ELF compatibility post-processing)
    │
    ▼
ziskemu (emulator — fast, no proof)         cargo-zisk prove (STARK proof)
```

We build `libziskos.a` directly from `0xPolygonHermez/zisk` upstream so we stay in sync with current CSR assignments and can add our own `poseidon2_c` trampoline for binary-trie state roots.

## How Zisk Precompiles Work

Zisk provides hardware-accelerated cryptographic operations via **CSR (Control Status Register) instructions**. The guest program writes a pointer to a parameter struct into a CSR register; the prover intercepts this, computes the result, and writes it back to the guest's memory.

There are two categories of CSR instructions:

### Regular precompiles (0x800–0x812, 0x817–0x819)

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
| 0x815 | `dma_inputcpy` | `csrs 0x815, dst; addi rd, dst, count` |
| 0x816 | `__wrap_memset` | `csrs 0x816, dst; addi x0, count, fill_byte` |

The DMA symbols come from Zisk's `ziskos` crate (patched to `__wrap_*` names — see Patch 6 below).

### Ethereum precompile → CSR mapping

Each Ethereum precompile is orchestrated by a Rust `_c` function inside `libziskos.a`, which in turn issues sequences of CSR calls:

| EVM Address | Precompile | Rust function (`libziskos.a`) | CSR calls used |
|-------------|-----------|-------------------|----------------|
| 0x01 | ECRECOVER | `secp256k1_ecdsa_address_recover_c` | secp256k1_add/dbl, arith256_mod, keccakf |
| 0x02 | SHA-256 | `sha256_c` | sha256_f |
| 0x03 | RIPEMD-160 | managed C# fallback | (none — pure software) |
| 0x04 | IDENTITY | managed C# | (none) |
| 0x05 | MODEXP | `modexp_bytes_c` | arith256_mod |
| 0x06 | BN254 ADD | `bn254_g1_add_c` | bn254_curve_add |
| 0x07 | BN254 MUL | `bn254_g1_mul_c` | bn254_curve_add/dbl |
| 0x08 | BN254 PAIRING | `bn254_pairing_check_c` | bn254_curve_add/dbl, bn254_complex_add/sub/mul, arith256_mod |
| 0x09 | BLAKE2F | `blake2b_compress_c` | blake2b_round |
| 0x0A | KZG | `verify_kzg_proof_c` | bls12_381_* |
| 0x0B–0x12 | BLS12-381 | `bls12_381_g1_add_c`, `_msm_c`, `_g2_*`, `_pairing_check_c`, `_fp_to_g1_c`, `_fp2_to_g2_c` | bls12_381_curve_add/dbl, bls12_381_complex_add/sub/mul, arith384_mod |
| 0x100 | P256VERIFY | `secp256r1_ecdsa_verify_c` | secp256r1_add/dbl, arith256_mod |

All `_c` functions are `extern "C"` Rust functions compiled into `libziskos.a`. They handle encoding/decoding, validation, and algorithm orchestration (sponge construction, scalar multiplication, Miller loops, etc.), delegating primitive field/curve operations to CSR instructions.

## Building `libziskos.a` — `zisk/scripts/build-libziskos.sh`

`libziskos.a` contains Zisk's compiled Rust guest runtime — all crypto orchestration, CSR wrappers, memory management, DMA operations — plus our `poseidon2_c` trampoline. The script runs entirely inside a Docker `rust:latest` container so no host Rust toolchain is required. Works on Windows (MSYS/Git Bash) and Linux.

```bash
bash zisk/scripts/build-libziskos.sh [zisk-git-ref]   # default ref: main
```

Output: `zisk/.libziskos/libziskos.a` + `libziskos.bflat.manifest`.

### Step 0 — Container setup

Inside `rust:latest`:

- `apt-get install gcc-riscv64-linux-gnu binutils-riscv64-linux-gnu python3`
- `rustup toolchain install nightly`
- `rustup component add rust-src --toolchain nightly` (needed for `-Z build-std`)

### Step 1 — Clone Zisk upstream

```bash
git clone --depth 1 --branch "$ZISK_REF" https://github.com/0xPolygonHermez/zisk.git
```

The known-good commit is pinned in the script as `ZISK_COMMIT_KNOWN` for reference; the script always records whatever commit it cloned into the output manifest.

### Step 2 — Apply seven patches

The Zisk `ziskos/entrypoint` crate is designed as a standalone zkVM runtime. To co-exist with bflat/NativeAOT (which provides its own `_start`, allocator, and `memcpy`), we need:

#### Patch 1 — Static library output

`ziskos/entrypoint/Cargo.toml` gets a `[lib] crate-type = ["staticlib", "rlib"]` section so `cargo build` produces `libziskos.a` instead of an executable.

#### Patch 2 — `no_entrypoint` feature flag

Add `no_entrypoint = []` to the `[features]` section. We'll compile with `--features no_entrypoint`.

#### Patch 3 — Guard `_start` and `_zisk_main`

Prefix both functions in `ziskos/entrypoint/src/lib.rs` with `#[cfg(not(feature = "no_entrypoint"))]` so they don't collide with bflat's own entrypoint.

#### Patch 4 — `sys_panic` stub

Append to `lib.rs`:

```rust
#[no_mangle]
extern "C" fn sys_panic(_msg_ptr: *const u8, _msg_len: usize) -> ! {
    loop {}
}
```

Zisk's own panic handler conflicts with bflat's; this stub satisfies Rust's abort panic strategy with a trivial spin loop.

#### Patch 5 — Replace bump allocator

In `ziskos/entrypoint/src/alloc/alloc.rs`, swap the inline bump allocator for a malloc wrapper that routes to bflat's allocator:

```rust
#[inline(never)]
pub unsafe fn inline_bump_alloc_aligned(bytes: usize, _align: usize) -> *mut u8 {
    extern "C" {
        fn __wrap___libc_malloc_impl(n: core::ffi::c_ulong) -> *mut core::ffi::c_void;
    }
    unsafe { __wrap___libc_malloc_impl(bytes as core::ffi::c_ulong) as *mut u8 }
}
```

This ensures both the Rust guest runtime and the .NET NativeAOT runtime share a single heap managed by bflat.

#### Patch 6 — Wrap DMA symbols

Rename four symbols in `ziskos/entrypoint/src/dma/{memcpy,memset,memmove,memcmp}.s`:

```
memcpy  → __wrap_memcpy
memset  → __wrap_memset
memmove → __wrap_memmove
memcmp  → __wrap_memcmp
```

bflat's linker is invoked with `--wrap memcpy --wrap memset --wrap memmove --wrap memcmp`. The linker redirects all `memcpy` calls from user code (including .NET's own codegen) to `__wrap_memcpy` inside `libziskos.a` — which issues the `csrs 0x813` DMA instruction. The original `memcpy` symbol becomes `__real_memcpy` if anything needs the raw C implementation (nothing in our build does).

This is the mechanism that lets us accelerate every `memcpy` in the final ELF (including those the .NET runtime emits for large struct copies) using Zisk's DMA CSR.

#### Patch 7 — Skip native C++ build for zkvm target

Zisk's `lib-c` crate has a `build.rs` that compiles x86 NASM for the host prover. On our zkvm target this is irrelevant and unbuildable — patch it to early-return:

```rust
if env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "zkvm" {
    println!("cargo:rustc-cfg=feature=\"no_lib_link\"");
    return;
}
```

### Step 3 — RISC-V target spec

The script writes `riscv64imad-zisk-zkvm-elf.json` with the exact target Zisk expects:

```json
{
  "llvm-target": "riscv64",
  "arch": "riscv64",
  "os": "zkvm",
  "vendor": "zisk",
  "features": "+m,+a,+d",
  "panic-strategy": "abort",
  "relocation-model": "static",
  "code-model": "medium",
  "disable-redzone": true,
  ...
}
```

Key points: `os=zkvm` / `vendor=zisk` is what Patch 7 pattern-matches, `panic-strategy=abort` matches our `sys_panic` stub, `code-model=medium` keeps text+data addressable by 32-bit offsets, `disable-redzone` avoids stack-below-sp tricks (Zisk has a fixed stack).

### Step 4 — Build the Rust crate

```bash
cargo +nightly build --release \
    --target /tmp/zisk/riscv64imad-zisk-zkvm-elf.json \
    -Z build-std=std,panic_abort \
    -Z json-target-spec \
    --features no_entrypoint
```

`-Z build-std=std,panic_abort` rebuilds `std` from source for our custom target (nightly-only). `--features no_entrypoint` activates Patches 2 and 3.

Output: `/tmp/zisk/target/riscv64imad-zisk-zkvm-elf/release/libziskos.a`.

### Step 5 — Add our `poseidon2_c` trampoline

Zisk upstream exports `syscall_poseidon2` (the raw Goldilocks permutation wrapper around CSR 0x812) but NOT a high-level `*_c` symbol for it. We need one so `ZiskPoseidonHashProvider` can call it via the same `[DllImport("__Internal")]` pattern as every other Rust `_c` function.

The script compiles a two-line C file inline and appends the resulting object directly into `libziskos.a`:

```c
/* /tmp/extras.c (written as a heredoc inside build-libziskos.sh) */
extern void syscall_poseidon2(unsigned long *state);
void poseidon2_c(unsigned long *state) { syscall_poseidon2(state); }
```

```bash
riscv64-linux-gnu-gcc -c -march=rv64ima -mabi=lp64 -O2 -fno-builtin /tmp/extras.c -o /tmp/extras.o
riscv64-linux-gnu-ar r "$BUILT" /tmp/extras.o
riscv64-linux-gnu-ranlib "$BUILT"
```

The `ar r` appends the object into the existing archive, so `libziskos.a` now exports `poseidon2_c` alongside all the other `_c` functions. No separate `.a`, no additional `--extlib` flag needed at EVM build time.

### Step 6 — Copy output + manifest

The script copies `libziskos.a` to `zisk/.libziskos/` and generates `libziskos.bflat.manifest`:

```json
{
  "name": "libziskos",
  "package_version": "1.0.0",
  "zisk_ref": "main",
  "zisk_commit": "<sha>",
  "source": "Zisk upstream + Nethereum poseidon2_c",
  "builds": [{
    "arch": "riscv64",
    "os": "linux",
    "libc": "zisk",
    "static_lib": "runtimes/linux-riscv64/native/libziskos.a"
  }],
  "wrap_symbols": ["memcpy", "memset", "memmove", "memcmp"]
}
```

The `wrap_symbols` array is read by bflat at link time and converted into `--wrap <sym>` linker flags — this is what activates Patch 6 routing at the final link stage.

### Step 7 — Verify symbols

```bash
riscv64-linux-gnu-nm libziskos.a | grep " T " | grep -E \
    "keccak256_c|sha256_c|poseidon2_c|bn254_pairing|secp256r1_ecdsa|blake2b_compress"
```

All six symbols must appear — if any is missing, the EVM link will fail with an unresolved-symbol error.

## Nethereum Native Bindings — `Nethereum.Zisk.Core.ZiskCrypto`

Every Rust `_c` function in `libziskos.a` is exposed to C# as a `[DllImport("__Internal")]` method. The `__Internal` library name tells NativeAOT to resolve the symbol statically from whatever `.a` files were linked — no runtime dynamic loading.

```csharp
public static class ZiskCrypto
{
    [DllImport("__Internal")]
    public static extern void keccak256_c(byte[] input, nuint input_len, byte[] output);

    [DllImport("__Internal")]
    public static extern void sha256_c(byte[] input, nuint input_len, byte[] output);

    [DllImport("__Internal")]
    public static extern byte secp256k1_ecdsa_address_recover_c(
        byte[] sig, byte recid, byte[] msg, byte[] output);

    [DllImport("__Internal")]
    public static extern nuint modexp_bytes_c(
        byte[] base_ptr, nuint base_len,
        byte[] exp_ptr, nuint exp_len,
        byte[] modulus_ptr, nuint modulus_len,
        byte[] result_ptr);

    [DllImport("__Internal")] public static extern byte bn254_g1_add_c(byte[] p1, byte[] p2, byte[] ret);
    [DllImport("__Internal")] public static extern byte bn254_g1_mul_c(byte[] point, byte[] scalar, byte[] ret);
    [DllImport("__Internal")] public static extern byte bn254_pairing_check_c(byte[] pairs, nuint num_pairs);

    [DllImport("__Internal")]
    public static extern void blake2b_compress_c(
        uint rounds, ulong[] state, ulong[] message, ulong[] offset, byte final_block);

    [DllImport("__Internal")] public static extern byte verify_kzg_proof_c(byte[] z, byte[] y, byte[] commitment, byte[] proof);
    [DllImport("__Internal")] public static extern byte bls12_381_g1_add_c(byte[] ret, byte[] a, byte[] b);
    [DllImport("__Internal")] public static extern byte bls12_381_g1_msm_c(byte[] ret, byte[] pairs, nuint num_pairs);
    [DllImport("__Internal")] public static extern byte bls12_381_g2_add_c(byte[] ret, byte[] a, byte[] b);
    [DllImport("__Internal")] public static extern byte bls12_381_g2_msm_c(byte[] ret, byte[] pairs, nuint num_pairs);
    [DllImport("__Internal")] public static extern byte bls12_381_pairing_check_c(byte[] pairs, nuint num_pairs);
    [DllImport("__Internal")] public static extern byte bls12_381_fp_to_g1_c(byte[] ret, byte[] fp);
    [DllImport("__Internal")] public static extern byte bls12_381_fp2_to_g2_c(byte[] ret, byte[] fp2);

    [DllImport("__Internal")] public static extern unsafe void poseidon2_c(ulong* state);
}
```

At runtime each call goes C# → NativeAOT P/Invoke thunk → statically-linked symbol in `libziskos.a` → CSR instruction(s) → prover intercept.

## Hash provider implementations (C#)

For the EIP-7864 binary state trie, we need `IHashProvider` implementations that work inside the zkVM guest:

| Provider | Rust function called | Sponge / schedule handling |
|----------|----------------------|----------------------------|
| `ZiskKeccakHashProvider` | `keccak256_c` → CSR 0x800 | Rust handles padding + sponge |
| `ZiskSha256HashProvider` | `sha256_c` → CSR 0x805 | Rust handles schedule + padding |
| `ZiskPoseidonHashProvider` | `poseidon2_c` → CSR 0x812 | C# sponge: width 16, rate 8, digest 4 × u64 |

Keccak and SHA-256 delegate entirely to the Rust `_c` functions which handle padding, sponge / message-schedule, and CSR calls. Poseidon2 is different: Zisk only exports the raw permutation, so `ZiskPoseidonHashProvider` does the Goldilocks sponge construction itself in C# (absorb 8 × u64 from input into state, call `poseidon2_c(state)` to permute, squeeze 4 × u64 from state for the digest).

`ZiskBinaryWitness.cs` routes the hash function at witness-read time:

```
Witness features.HashFunction →
    Blake3    → Blake3HashProvider (managed, no CSR)
    Poseidon  → ZiskPoseidonHashProvider (C# sponge + CSR 0x812)
    Sha256    → ZiskSha256HashProvider  (CSR 0x805 via Rust sha256_c)
    Keccak    → ZiskKeccakHashProvider  (CSR 0x800 via Rust keccak256_c)
```

## Building the EVM ELF — `zisk/scripts/build.sh`

```bash
LIBZISKOS_DIR="$(pwd)/zisk/.libziskos" bash zisk/scripts/build.sh
```

Two modes:

| Mode | Command | Trade-off |
|------|---------|-----------|
| `--source` (default) | `bash zisk/scripts/build.sh` | bflat compiles all `.cs` sources in one invocation — smaller ELF, cross-assembly inlining works. Production builds. |
| `--dll` | `bash zisk/scripts/build.sh --dll` | `dotnet build` produces a DLL first, bflat links it — faster iteration, ~25% larger ELF (no cross-assembly inlining). |

### What source-mode compiles

The script hand-picks only what the EVM needs (avoiding LINQ, `System.Runtime.Numerics`, Newtonsoft, etc.):

- **`Nethereum.Zisk.Core`** — whole project (ZiskIO, ZiskInput, ZiskCrypto, hash providers)
- **`Nethereum.EVM.Zisk`** — `Zisk/*.cs` + `Zisk/Backends/**/*.cs` (witness reader, precompile backends, state root resolver)
- **`Nethereum.Util`** — `EvmUInt256`, `EvmInt256`, `AddressUtil`, `AddressExtensions`, `ContractUtils`, `Sha3Keccack`, `ByteUtil`, `EvmUInt256RLPExtensions`, `KeccakDigest`, hash provider interfaces, Poseidon core + presets + params
- **`Nethereum.Merkle.Binary`** — whole project (EIP-7864 binary trie, state-root calculator)
- **`Nethereum.Hex`** — `HexByteConvertorExtensions`
- **`Nethereum.EVM.Core`** — whole project
- **`Nethereum.Merkle.Patricia`** — whole project except `*ProofVerification*`
- **`Nethereum.CoreChain`** — `PatriciaStateRootCalculator`, `PatriciaMerkleTreeBuilder`, `PatriciaBlockRootCalculator`, `BinaryStateRootCalculator`
- **`Nethereum.Model`** — selected files (signed transaction types, access list, authorisation, RLP encoders)
- **`Nethereum.RLP`** — whole project

### Step 1 — `bflat build`

```
bflat build $SRC \
    --os linux --arch riscv64 --libc zisk \
    --no-globalization --no-pthread --no-stacktrace-data \
    --no-exception-messages \
    -Os --no-pie \
    -d EVM_SYNC \
    --extlib /libziskos/libziskos.bflat.manifest \
    -o /src/zisk/output/nethereum_evm_raw
```

- `--libc zisk` — use Zisk's libc shims (provided by the bflat Docker image)
- `--no-globalization --no-pthread --no-stacktrace-data --no-exception-messages` — drop runtime features that pull in too much code
- `-Os --no-pie` — optimise for size, position-dependent (zkVM is static)
- `-d EVM_SYNC` — enable the sync EVM engine (the async one pulls in `System.Threading.Tasks` which is huge)
- `--extlib` — the libziskos manifest we built in `build-libziskos.sh`; bflat reads it to locate the static lib AND the `wrap_symbols` array

### Step 2 — `patch_elf`

```
patch_elf nethereum_evm_raw nethereum_evm_elf \
    --fix-init-array --fix-tdata --remove-eh --split-code-data
```

- `--fix-init-array` — rewrites `.init_array` so .NET static constructors run in correct order
- `--fix-tdata` — normalises thread-local data (Zisk is single-threaded but TLS section layout must be sane)
- `--remove-eh` — strips `.eh_frame` and `.dotnet_eh` (no exception unwinding in Zisk)
- `--split-code-data` — separates `.text` from `.rodata` so Zisk's ROM/RAM split works

### Step 3 — `signal_patch`

```
signal_patch nethereum_evm_elf
```

Binary-patches the first instructions of `__block_app_sigs`, `__reset_app_sigs`, etc. to `ret` (0x00008067) so .NET's signal-handler setup becomes a no-op. Zisk has no signals.

## Host-side tooling — `zisk/scripts/setup-host.sh`

One-shot installer for a clean Ubuntu/WSL2 host. Steps:

1. **System deps** — `libomp5-14 libomp-dev gcc-riscv64-linux-gnu libicu-dev` + optional `libomp.so.5` symlink
2. **.NET 10 SDK** — `dotnet-install.sh` to `/usr/lib/dotnet` if not already present
3. **Rust** — rustup via the official installer (needed only for `build-libziskos.sh` if run outside Docker; Docker path doesn't need host Rust)
4. **Zisk** — downloads `ziskup` to `~/.zisk/bin`, runs `ziskup --nokey` (emulator + prover, no proving key download)
5. **Verify** — reports installed versions of .NET, Rust, cargo-zisk, RISC-V gcc
6. **Clone `Nethereum/bflat-riscv64`** into `~/tools/` (nethereum branch)
7. **Build the bflat Docker image** — `docker build -t nethereum/bflat-riscv64 .` from the cloned repo

After this, the host is ready to run `build-libziskos.sh` and `build.sh`.

## Prerequisites

- **Docker** (Desktop on Windows/macOS, or daemon on Linux)
- **.NET 10 SDK** (only for `--dll` build mode)
- **Zisk emulator + prover** (installed via `setup-host.sh`) — only needed to run/prove the binary, not to build it

No host-side RISC-V toolchain or Rust toolchain is needed — everything that compiles RISC-V runs in Docker.

## Quick Start

### 1. Build the Docker image

```bash
git clone --branch nethereum https://github.com/Nethereum/bflat-riscv64.git ../bflat-riscv64
cd ../bflat-riscv64 && docker build -t nethereum/bflat-riscv64 .
```

Or use the setup script: `bash zisk/scripts/setup-host.sh`.

### 2. Build libziskos.a from Zisk upstream (one-time)

```bash
bash zisk/scripts/build-libziskos.sh main
```

Clones Zisk, applies seven patches, compiles the Rust crate for RISC-V, adds our `poseidon2_c` trampoline, and outputs `zisk/.libziskos/libziskos.a`.

### 3. Compile the EVM to a Zisk-ready ELF

```bash
LIBZISKOS_DIR="$(pwd)/zisk/.libziskos" bash zisk/scripts/build.sh
```

Output: `zisk/output/nethereum_evm_elf` (~5 MB).

### 4. Generate a witness

```bash
dotnet test tests/Nethereum.EVM.Core.Tests --filter "GenerateSimpleSstoreWitness"
```

Produces `zisk/output/test_sstore.bin` — a serialised `BinaryBlockWitness` with a block, one SSTORE transaction, pre-state accounts, and the signed transaction RLP.

### 5. Run in the emulator

```bash
# WSL on Windows:
wsl -d Ubuntu -- ~/.zisk/bin/ziskemu -e /mnt/c/.../zisk/output/nethereum_evm_elf \
    --legacy-inputs /mnt/c/.../zisk/output/test_sstore.bin -n 500000000

# Linux:
ziskemu -e zisk/output/nethereum_evm_elf --legacy-inputs zisk/output/test_sstore.bin -n 500000000
```

Expected output:

```
BIN:reading
BIN:block txs=1 accounts=2
BIN:exec
BIN:executed ok=1 fail=0
BIN:state_root=0x2fc84afa1e66eaef44a179f33c5a2bbacfa458bdc97b12cd7c3c29750dd8142d
BIN:OK gas=43106
```

### 6. Run precompile tests

```bash
dotnet test tests/Nethereum.EVM.Core.Tests --filter "ZiskEmu_stPreCompiledContracts"
```

## Environment overrides

- `ZISK_IMAGE=<name>` — use a different bflat Docker image
- `LIBZISKOS_DIR=<host-path>` — directory containing `libziskos.bflat.manifest` (typically `zisk/.libziskos/` after running `build-libziskos.sh`)

## Common pitfalls when upgrading

### `LIBZISKOS_DIR` must point at a libziskos that exports every `ZiskCrypto.cs` symbol

`libziskos.a` is our own build (via `build-libziskos.sh`), but its contents come from Zisk upstream Rust. When `ZiskCrypto.cs` adds a `[DllImport("__Internal")]` for a new precompile symbol (e.g. `zkvm_secp256r1_verify`, `zkvm_bls12_*`), the symbol must exist in the `libziskos.a` we link against.

The bflat Docker image bundles a bootstrapping libziskos used only if `LIBZISKOS_DIR` is unset. That bundle is whatever was current when the image was built — it goes stale as soon as we wire new symbols. Symptoms when stale:

```
lld: error: undefined symbol: zkvm_bls12_g2_add
lld: error: undefined symbol: zkvm_secp256r1_verify
lld: error: undefined symbol: poseidon2_c
```

Always pass `LIBZISKOS_DIR="$(pwd)/zisk/.libziskos"` and rebuild it (`bash zisk/scripts/build-libziskos.sh <ref>`) when you wire new precompile bindings or bump the Zisk source ref.

### ELF build output ≠ test runner read path

| Path | Used by |
|------|---------|
| `zisk/output/nethereum_evm_elf` | Where `build.sh` writes |
| `scripts/zisk-output/nethereum_evm_elf` | Where `ZiskStateTestRunner.FindElfPath()` reads |

After every `build.sh`, copy: `cp zisk/output/nethereum_evm_elf scripts/zisk-output/nethereum_evm_elf` — otherwise emulator tests run yesterday's bytecode against today's expectations.

### bflat source set is hand-curated — changes outside it don't affect the ELF

`build.sh` lists exactly which `.cs` files get compiled into the guest ELF. Anything outside this list is invisible to the proof pipeline. The non-obvious exclusions:

- `Nethereum.CoreChain/BlockExecutor.cs`, `TransactionProcessor.cs`, `BlockContext.cs`, `IncrementalStateRootCalculator.cs` — **NOT** in scope (only 4 specific state-root calculators are pulled)
- `Nethereum.EVM.Precompiles.Kzg/KzgAwareMainnetHardforkRegistry.cs` — NOT in scope (intentional: pulls BCL IO + embedded trusted-setup loading)
- Of `Nethereum.Model/`, only the ~40 files explicitly listed (not `P2P/`, not `Network/`)

If your change feels like it should be exercised by ZiskEmu tests but isn't, check whether the file is in the `SRC=` list of `build.sh`.

### `ziskup --nokey` pulls v0.16.1, not the latest tag

Upstream's "latest" GitHub Release pointer is stale relative to tagged commits. To get a specific version, download directly:

```bash
curl -sL -o zisk-v0.18.0.tar.gz \
  https://github.com/0xPolygonHermez/zisk/releases/download/v0.18.0/cargo_zisk_linux_amd64.tar.gz
```

### v0.18.0+ splits binaries into CPU and GPU variants

v0.18.0 ships `cargo-zisk-cpu` and `cargo-zisk-gpu` instead of a unified `cargo-zisk`. Same for `zisk-worker`. On headless servers without a GPU, symlink the CPU variants:

```bash
cd ~/.zisk/bin
ln -sf cargo-zisk-cpu cargo-zisk
ln -sf zisk-worker-cpu zisk-worker
```

### `build-libziskos.sh` Patch 1 silently skips for Zisk v0.18.0+

v0.18.0 ships `ziskos/entrypoint/Cargo.toml` with a pre-existing `[lib]\ncrate-type = ["rlib"]`. The script's guard `if ! grep -q "^\[lib\]"` matches this and skips the staticlib add, so cargo only emits `.rlib` and the build fails later with `ERROR: libziskos.a not found`. Until the patch is updated to merge into an existing `crate-type` line, either:

- Pin to a pre-v0.18.0 ref: `bash zisk/scripts/build-libziskos.sh pre-develop-0.17.1`
- Or edit `ziskos/entrypoint/Cargo.toml` in your clone to add `"staticlib"` before re-running

## Directory layout

```
zisk/
├── README.md                 # This file
├── .libziskos/               # Self-built from Zisk upstream (gitignored)
│   ├── libziskos.a           # Rust crypto + syscalls + DMA + our poseidon2_c
│   ├── libziskos.bflat.manifest
│   └── runtimes/linux-riscv64/native/libziskos.a
├── scripts/
│   ├── build.sh              # EVM → ELF build (source + DLL modes)
│   ├── build-libziskos.sh    # Build libziskos.a from Zisk upstream in Docker
│   ├── prepare-input.py      # Build a Zisk input file from raw data chunks
│   ├── analyze-elf.sh        # Diagnostic: dump text symbol sizes
│   ├── analyze-sections.sh   # Diagnostic: section sizes + flags
│   └── setup-host.sh         # Host-side install (system deps, .NET, Rust, Zisk, bflat image)
├── test-artifacts/           # Pre-built EVM binary, witnesses, and verified proofs
│   ├── evm_lazy_nocookie     # RISC-V ELF (5.6MB)
│   ├── witness_*_standard.bin # 4 witness variants (standard input format)
│   └── proofs/               # 4 verified STARK proofs (329KB each)
└── output/                   # ELF + witness artefacts (gitignored)
```

## Validated test results

| Witness | State Root | Status |
|---------|-----------|--------|
| Patricia + Keccak | `0x2fc84afa1e66eaef44a179f33c5a2bbacfa458bdc97b12cd7c3c29750dd8142d` | BIN:OK |
| Binary + Blake3 | `0x937dca986d82499f329a7277aa4ab5f80657e8d3674ba49066d95f8e26039f5b` | BIN:OK |
| Binary + Poseidon2 | `0x8501b6a2d153c13a57271324a15bf888c129ed4b5e3e08b23b2ef7d7e8d557a9` | BIN:OK |
| stPreCompiledContracts (identity, modexp) | state roots match managed .NET | PASSED |
| stPreCompiledContracts2 (ecrecover, sha256, ripemd, bn254) | state roots match managed .NET | PASSED |

State roots are cross-validated: the ziskemu output matches the managed .NET EVM using `PatriciaStateRootCalculator` / `BinaryStateRootCalculator` with managed hash providers.

## What the `Nethereum/bflat-riscv64` fork adds

The `nethereum` branch of [Nethereum/bflat-riscv64](https://github.com/Nethereum/bflat-riscv64/tree/nethereum) carries patches on top of the upstream bflat compiler:

| Patch | File | Why |
|-------|------|-----|
| TSS reduction | `src/bflat/modules/rhp/module.c` — `TSS_MAX_TYPEMANAGERS 1024→32`, `TSS_MAX_SLOTS 4096→256` | Default values bloat `.bss` to 33.8 MB, exceeding Zisk's 32 MB ROM limit. Reduced to 320 KB. |
| `calloc` → `malloc` + `memset` | same file — all `__wrap_Rhp*` allocators | `pal.o` wraps `malloc` via `--wrap` but not `calloc`; the default `calloc` path produces unresolved `__real_calloc` in a Zisk build. |
| `signal_patch` | `docker/signal_patch.sh` | Binary-patches signal setup functions to `ret` so the Zisk ELF doesn't invoke unsupported syscalls. |
| Entrypoint dispatch | `docker/entrypoint.sh` | Exposes `bflat`, `patch_elf`, `signal_patch` as top-level Docker commands. |

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

# 2. BSS size sane (~2 MB, not ~35 MB)
riscv64-linux-gnu-size -A zisk/output/nethereum_evm_elf | grep '\.bss'

# 3. No System.Runtime.Numerics (BigInteger library not pulled in)
riscv64-linux-gnu-nm zisk/output/nethereum_evm_elf | grep System_Runtime_Numerics

# 4. Signal functions patched (first instruction: ret / 00008067)
riscv64-linux-gnu-objdump -d zisk/output/nethereum_evm_elf | grep -A1 "<__block_app_sigs>:"

# 5. Emulator runs
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

Auto-detection in the RISC-V binary (`ZiskInput.Read()`): checks byte at offset 16 — if zero → standard, if non-zero → legacy. This works when the tool passes data correctly, but **the tool must frame it properly**.

- `ziskemu --legacy-inputs file.bin` → reads file, prepends legacy header, maps to memory
- `cargo-zisk prove -i file.bin` → reads file AS-IS, maps to memory (file must already be in standard format)
- **WSL2 users**: Zisk's prover needs ≥28 GB allocated to the WSL VM. Add to `%USERPROFILE%\.wslconfig`:
  ```ini
  [wsl2]
  memory=28GB
  swap=8GB
  ```
  Then `wsl --shutdown` to apply.

## Zisk constraint fixes (Nethereum/zisk fork)

NativeAOT-compiled ELFs trigger several Zisk constraint violations that don't appear with standard Rust/Go programs. We maintain a fork at [Nethereum/zisk](https://github.com/Nethereum/zisk/tree/fix/nativeaot-constraints) with the following fixes.

### Bug #1 — `get_read_value` double shift in `mem_helpers.rs`

**File:** `state-machines/mem-common/src/mem_helpers.rs` line 146

The cross-word-boundary read value computation had an extra `>> offset`:

```rust
// BEFORE (wrong):
value |= read_values[1] << (64 - offset) >> offset;
// AFTER (correct):
value |= read_values[1] << (64 - offset);
```

This caused ~104 MemAlign constraint violations for any program with unaligned 8-byte reads crossing a word boundary.

### Bug #2 — Read-before-write in MemAlign `TwoWrites` path

**File:** `state-machines/mem/src/mem_align_sm.rs`

The TwoWrites path read values from `first_write_row.get_reg(i)` and `second_write_row.get_reg(i)` before those rows were populated. Fixed by computing values directly from `value_first_write` and `value_second_write` using `Self::get_byte()`.

### Bug #3 — Overlapping section merge in `elf_extraction.rs`

**File:** `core/src/elf_extraction.rs`

When `patch_elf --split-code-data` creates a `.text_overlay` section, LIEF (the ELF library used by `patch_elf`) pads it past its declared size, overlapping into `.rodata`. The `merge_adjacent_ro_sections` function had no overlap handling — it would merge overlapping sections, corrupting rodata bytes. Fixed by truncating the first section at the overlap point:

```rust
} else if current_end > section.addr {
    let overlap = (current_end - section.addr) as usize;
    let new_len = current.data.len().saturating_sub(overlap);
    current.data.truncate(new_len);
    if !current.data.is_empty() { merged.push(current); }
    current = section;
}
```

This fixed 447/451 RomData constraint violations.

### Bug #5 — `is_full_aligned` checking 4-byte instead of 8-byte alignment

**File:** `core/src/mem.rs` (3 locations)

The alignment check used `0x03` (4-byte mask) instead of `0x07` (8-byte mask):

```rust
// BEFORE (wrong):
((addr & 0x03) == 0) && (width == 8)
// AFTER (correct):
((addr & 0x07) == 0) && (width == 8)
```

An 8-byte read at address `0xAFFF1B04` passed the 4-byte alignment check but actually crossed a word boundary, causing 18/160 `read_same_addr` Mem SM violations.

### Cookie NOP — `__security_cookie` write

**Problem:** NativeAOT's `RhInitialize` writes a non-deterministic stack canary (`__security_cookie`) to `.rodata` at startup. This value differs between emulator runs and prover runs, causing 1 RomData constraint violation.

**Fix:** Binary-patch the `sd a0, 0(s1)` instruction that writes the cookie to a NOP (`0x00000013`). The cookie address varies per binary — find it via disassembly:

```bash
riscv64-linux-gnu-objdump -d binary | grep -B20 'minipal_lowres_ticks' | grep 'sd.*a0.*s1'
```

Pattern: `addi s1, aX, <cookie_offset>` → `call minipal_lowres_ticks` → `sd a0, 0(s1)` ← NOP this last instruction.

### Float library rebuild

**Problem:** Zisk's prebuilt `ziskfloat.elf` (110,208 bytes) has a bus accounting bug that causes the Global constraint to fail for ALL programs.

**Fix:** Rebuild from source: `cd lib-float/c && make`. Produces 112,120 bytes with correct bus accounting. Must be done on every fresh Zisk checkout.

### Bug #5 — Hardcoded `bytes=8` in cross-word-boundary sub-word bus payload

**File:** `emulator/src/emu.rs` (2 locations: read path line 847, write path line 1281)

In `source_b_mem_reads_consume_databus()`, the `SRC_IND` double_not_aligned paths hardcoded `8` in the bus payload width field instead of using `instruction.ind_width`. Sub-word operations (LW/LH/LB) at addresses where `offset + width > 8` (crossing an 8-byte word boundary) emitted `bytes=8` on the bus, but Main PIL emits assumes with the actual width (4, 2, 1). MemAlign processed these as width=8 TwoReads, so its bus entries never matched Main's sub-word assumes.

```rust
// BEFORE (wrong):
let payload = MemHelpers::mem_load(address as u32, ..., 8, [raw_data_1, raw_data_2]);
// AFTER (correct):
let payload = MemHelpers::mem_load(address as u32, ..., instruction.ind_width as u8, [raw_data_1, raw_data_2]);
```

**Diagnosis:** `cargo-zisk verify-constraints -d` reports `opids do not match [10]` (MEMORY_ID). Full debug mode shows unmatched assumes at `bytes=4` and unmatched proves at `bytes=8` with identical addresses and steps.

**Impact:** This was the root cause of the previously reported multi-instance global constraint failure. It was never a multi-instance issue — it was a bus accounting bug that only manifested when programs did enough sub-word unaligned memory access (which correlated with higher step counts).

## Proving end-to-end

With all 5 fixes applied (our Zisk fork at `Nethereum/zisk` branch `fix/nativeaot-constraints` + cookie NOP + float rebuild):

```bash
export HWLOC_COMPONENTS=-gl

# 1. Verify all constraints pass (fast check, no proof generated)
cargo-zisk verify-constraints \
    -e zisk/test-artifacts/evm_lazy_nocookie \
    -i zisk/test-artifacts/witness_eth_transfer_noroot_standard.bin -l -d

# 2. Generate STARK + VADCOP proof
cargo-zisk prove \
    -e zisk/test-artifacts/evm_lazy_nocookie \
    -i zisk/test-artifacts/witness_eth_transfer_noroot_standard.bin -l -n \
    -o evm_proof

# 3. Verify proof
cargo-zisk verify -p evm_proof

# Or verify the pre-built proofs (no proving key needed):
cargo-zisk verify -p zisk/test-artifacts/proofs/proof_eth_transfer_noroot
```

**Test results (2026-05-06, 12-core server, 128GB RAM):**

| Witness | Steps | Main instances | Prove time | Proof size | Verify time |
|---------|-------|---------------|------------|------------|-------------|
| ETH transfer (no root) | 3,963,526 | 1 | 225s | 329KB | 187ms |
| SSTORE (no root) | 5,267,838 | 2 | 255s | 329KB | 187ms |
| ETH transfer (patricia) | 6,593,834 | 2 | 257s | 329KB | 187ms |
| SSTORE (patricia) | 8,044,287 | 2 | 255s | 329KB | 187ms |

Pre-built proofs and witnesses are in `zisk/test-artifacts/`.

**Phase breakdown (ETH transfer, 3.96M steps):**

| Phase | Time | Notes |
|-------|------|-------|
| Execution | ~0.6s | 3.96M steps, 12 air instances |
| Contributions | ~30s | Witness polynomial computation |
| Inner STARK proofs | ~189s | Per-air FRI proofs |
| VADCOP aggregation | ~5s | Recursive proof composition |
| **Total** | **~225s** | **329KB proof file** |

## Generating a provingKey from source

The official `ziskup` installs a pre-built provingKey. To generate one yourself (e.g. for a newer Zisk version before official keys are published), follow these steps.

### Prerequisites

```bash
# 1. pil2-compiler (JavaScript PIL → protobuf compiler)
git clone https://github.com/0xPolygonHermez/pil2-compiler /tmp/pil2-compiler
cd /tmp/pil2-compiler && npm install

# 2. pil2-proofman-js (provingKey generator)
git clone https://github.com/0xPolygonHermez/pil2-proofman /tmp/pil2-proofman-js
cd /tmp/pil2-proofman-js && npm install

# 2b. Fix missing PIL standard library for recursive setup
# The stark-recurser npm package is missing std PIL files needed by circom2pil.
# Copy them from the Rust proofman checkout (created when building Zisk):
STD_PIL=$(find ~/.cargo/git/checkouts/pil2-proofman-* -path "*/pil2-components/lib/std/pil" | head -1)
cp $STD_PIL/*.pil /tmp/pil2-proofman-js/node_modules/stark-recurser/src/circom2pil/pil/

# 3. Zisk source (contains PIL definitions)
git clone https://github.com/0xPolygonHermez/zisk /tmp/zisk
cd /tmp/zisk
```

### Step 1: Compile PIL (with fixed-to-file optimization)

The PIL compiler converts `.pil` files into a protobuf `pilout.bin`. Without optimization it needs 115+ GB of RAM because it embeds all fixed columns in the protobuf. The `-f` flag writes fixed columns to separate files instead, reducing memory to ~8 GB.

```bash
cd /tmp/zisk

# Find the proofman PIL include path (matches Cargo.lock commit)
PROOFMAN_PIL=$(find ~/.cargo/git/checkouts/pil2-proofman-* -path "*/pil2-components/lib/std/pil" | head -1)

# Compile — the key flags are:
#   -f <dir>           Write fixed columns to separate files (CRITICAL for memory)
#   -O no-proto-fixed-data   Skip embedding fixed data in protobuf
mkdir -p /tmp/pil_fixed_out
node --max-old-space-size=32000 /tmp/pil2-compiler/src/pil.js \
    pil/zisk.pil \
    -I $PROOFMAN_PIL,pil,state-machines,precompiles \
    -o /tmp/pilout_nofixed.bin \
    -n zisk \
    -f /tmp/pil_fixed_out \
    -O no-proto-fixed-data
```

**Expected:** ~4 minutes, ~8 GB RAM, produces `pilout_nofixed.bin` (4.3 MB) + fixed files in `/tmp/pil_fixed_out/` (~3.2 GB total).

**Without `-f`:** The compiler tries to hold all fixed columns in V8 heap. At column ~309 of ~320, it OOMs at 115+ GB. The `-f` flag is the fix — it streams each column to disk immediately.

### Step 2: Generate provingKey (basic + recursive)

```bash
# stdPath = circom standard library for recursive proofs
STD_PATH=/tmp/pil2-proofman-js/node_modules/stark-recurser/src/pil2circom/circuits.gl

# The key flags are:
#   -r                  Enable recursive/aggregation setup (compressor + vadcop)
#   -t <stdPath>        Circom standard library path (required with -r)
#   -u <fixedPath>      Point to fixed column files from Step 1
#   --stack-size=65536  Prevent stack overflow in calculateExpDeg recursion
node --max-old-space-size=64000 --stack-size=65536 \
    /tmp/pil2-proofman-js/src/main_setup.js \
    -a /tmp/pilout_nofixed.bin \
    -b /tmp/new_proving_key \
    -u /tmp/pil_fixed_out \
    -r \
    -t $STD_PATH
```

**Without `-r`:** Only generates basic per-air STARK setup. The prover will panic looking for `compressor/compressor.starkinfo.json` and `vadcop_final/vadcop_final.verkey.bin`. You MUST use `-r` for a usable provingKey.

**Expected output:** `/tmp/new_proving_key/provingKey/` containing:
- `pilout.globalConstraints.bin` + `.json` — global constraint formulas
- `pilout.globalInfo.json` — air metadata and sizes
- `zisk/Zisk/airs/<AirName>/air/` — per-air STARK keys
- `zisk/Zisk/airs/<AirName>/compressor/` — per-air compressor circuits
- `zisk/Zisk/airs/<AirName>/recursive1/` + `recursive2/` — recursive proof circuits
- `zisk/vadcop_final/` — VADCOP aggregation circuit
- `zisk/vadcop_final_compressed/` — compressed final circuit

### Step 3: Install the provingKey

```bash
# Back up existing key
cp -r ~/.zisk/provingKey ~/.zisk/provingKey.bak

# Install new key
rm -rf ~/.zisk/provingKey
cp -r /tmp/new_proving_key/provingKey ~/.zisk/provingKey

# Regenerate constant trees (one-time, done automatically on first prove)
HWLOC_COMPONENTS=-gl cargo-zisk check-setup -a
```

### Key generation timeline

| Phase | Time | Memory | Notes |
|-------|------|--------|-------|
| PIL compile | ~4 min | ~8 GB | With `-f` flag |
| Basic setup (35 airs) | ~2 min | ~1 GB | Per-air STARK keys |
| Recursive setup (compressor + circom) | ~30-60 min | ~4 GB | Circom compilation per air |
| First prove (const tree regen) | ~2 min | ~6 GB | One-time on first use |

### Proving cost profile (v16 binary, 12-core CPU, May 2026)

Base cost is **293.6M (constant)** — ELF initialization, NativeAOT runtime bootstrap, static constructors. This is independent of what the transaction does.

| Scenario | Steps | Base % | Main % | Opcodes % | Precompiles % | Memory % | Prove Time |
|----------|-------|--------|--------|-----------|---------------|----------|------------|
| eth_transfer (noroot) | 3.47M | 48.0% | 38.5% | 7.8% | 0.0% | 5.6% | 227s |
| sstore (noroot) | 4.77M | 40.0% | 44.3% | 9.0% | 0.0% | 6.7% | 256s |
| sstore (patricia root) | 7.55M | 29.7% | 51.9% | 10.5% | 0.0% | 7.9% | 257s |
| sstore (poseidon2 root) | 7.79M | 29.1% | 52.5% | 10.5% | 0.01% | 7.9% | 283s |
| ecrecover | 8.08M | 28.3% | 52.9% | 10.7% | 0.07% | 8.1% | 312s |
| bn128 pairing | 8.13M | 28.2% | 53.0% | 10.7% | 0.00% | 8.1% | 282s |
| multi_tx_mixed (3tx) | 12.87M | 19.9% | 59.3% | 12.2% | 0.0% | 8.6% | 340s |

Poseidon2 CSR cost: **66K** (0.01%) — hardware-accelerated, effectively free.
ECRECOVER CSR cost: **690K** (0.07%) — secp256k1 curve math via CSR.
BN128 pairing CSR cost: **5.7K** (0.00%) — heavy computation is in Main (Rust orchestration).
