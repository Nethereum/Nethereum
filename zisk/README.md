# Nethereum EVM → Zisk zkVM

Build tooling for compiling the Nethereum EVM to a RISC-V 64-bit ELF binary that runs inside the [Zisk](https://github.com/0xPolygonHermez/zisk) zero-knowledge virtual machine. This is the pipeline Nethereum uses to prove Ethereum execution: C# EVM → NativeAOT → RISC-V → Zisk → STARK proof.

## Architecture

```
C# source (Nethereum.EVM.Core, Nethereum.EVM.Zisk, Nethereum.Zisk.Core)
    │
    ▼
bflat-riscv64 (NativeAOT → RISC-V cross-compiler, in Docker)
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

Everything upstream of `build.sh` — the bflat compiler, the patched .NET 10 runtime, libziskos, the `rhp.o` runtime helper with our TSS + calloc patches, `patch_elf.py`, and `signal_patch.sh` — is contained in the [`Nethereum/bflat-riscv64` fork on the `nethereum` branch](https://github.com/Nethereum/bflat-riscv64/tree/nethereum). The image produced from that repo is `nethereum/bflat-riscv64`; everything here is orchestration on top of it.

## Prerequisites

- **Docker** (Desktop on Windows/macOS, or daemon on Linux).
- **.NET 10 SDK** (only needed for `--dll` build mode).
- **Zisk** (emulator + prover, installed via `scripts/setup-host.sh`). Optional — only required to run / prove the binary, not to build it.

No host-side RISC-V toolchain is needed — everything runs in the Docker image.

## Quick Start

### 1. Get the image

Either run the setup script (which clones the fork and builds the image):

```bash
bash zisk/scripts/setup-host.sh
```

or build it manually:

```bash
git clone --branch nethereum https://github.com/Nethereum/bflat-riscv64.git
cd bflat-riscv64 && docker build -t nethereum/bflat-riscv64 .
```

### 2. Compile the EVM to a Zisk-ready ELF

```bash
bash zisk/scripts/build.sh                    # source mode (default, smaller ELF)
bash zisk/scripts/build.sh --dll nethereum_evm # DLL mode (faster iteration, ~25% larger)
```

Output lands in `zisk/output/`:

| Artifact | Description |
|----------|-------------|
| `<name>_raw`  | Raw bflat output before post-processing |
| `<name>_elf`  | Patched ELF ready for `ziskemu` and `cargo-zisk prove` |

### 3. Run or prove

```bash
# Emulate (fast, no proof)
ziskemu -e zisk/output/nethereum_evm_elf -n 10000000

# Emulate with a witness input
ziskemu -e zisk/output/nethereum_evm_elf --legacy-inputs -i witness.bin

# Full STARK proof (requires ≥28 GB RAM allocated to WSL/Linux)
cargo-zisk convert-input -i witness.bin -o witness_std.bin
cargo-zisk prove -e zisk/output/nethereum_evm_elf -l -i witness_std.bin -o ./proof -n
```

## Build Modes

| Mode | Command | Trade-off |
|------|---------|-----------|
| `--source` (default) | `bash zisk/scripts/build.sh` | bflat compiles all `.cs` sources directly — smaller ELF, slower rebuild. Production builds. |
| `--dll` | `bash zisk/scripts/build.sh --dll` | `dotnet build` produces the DLL first, bflat links it — faster iteration, ~25% larger ELF (no cross-assembly inlining). |

### Environment overrides

- `ZISK_IMAGE=<name>` — use a different image (e.g. when testing a locally patched fork).
- `LIBZISKOS_DIR=<host-path>` — mount a host-built `libziskos` over the one in the image. The directory must contain `libziskos.bflat.manifest`; it's mounted as `/libziskos` and passed via `--extlib`. Useful when iterating on precompile backends in `bflat-libziskos`.

## Directory Layout

```
zisk/
├── README.md            # This file
├── scripts/
│   ├── build.sh         # Main entry point (source + DLL modes, calls Docker)
│   ├── prepare-input.py # Build a Zisk input file from raw data chunks
│   ├── analyze-elf.sh   # Diagnostic: dump text symbol sizes
│   ├── analyze-sections.sh # Diagnostic: section sizes + flags
│   └── setup-host.sh    # Host-side install (Zisk + clone + build image)
└── output/              # ELF artefacts (gitignored)
```

## What the Nethereum `bflat-riscv64` Fork Adds

The `nethereum` branch of [Nethereum/bflat-riscv64](https://github.com/Nethereum/bflat-riscv64/tree/nethereum) carries a small set of patches on top of Nethermind's fork:

| Patch | File | Why |
|-------|------|-----|
| TSS reduction | `src/bflat/modules/rhp/module.c` — `TSS_MAX_TYPEMANAGERS 1024→32`, `TSS_MAX_SLOTS 4096→256` | Default values bloat `.bss` to 33.8 MB, exceeding Zisk's 32 MB ROM limit. Reduced to 320 KB. |
| `calloc` → `malloc` + `memset` | same file — all `__wrap_Rhp*` allocators | Nethermind's `pal.o` wraps `malloc` via `--wrap` but not `calloc`; the default `calloc` path produces unresolved `__real_calloc` in a Zisk build. |
| `signal_patch.sh` | `docker/signal_patch.sh` | Binary-patches signal setup functions to `ret` so the Zisk ELF doesn't invoke unsupported syscalls. |
| Entrypoint dispatch | `docker/entrypoint.sh` | Exposes `bflat`, `patch_elf`, `signal_patch` as top-level Docker commands. Our `build.sh` calls these directly. |

## Verification

After a successful build, verify the ELF:

```bash
# 1. EH sections removed (eh_frame / dotnet_eh → none)
riscv64-linux-gnu-readelf -S zisk/output/nethereum_evm_elf | grep -E "eh_frame|dotnet_eh"

# 2. BSS size sane (~2 MB, not ~35 MB)
riscv64-linux-gnu-size -A zisk/output/nethereum_evm_elf | grep '\.bss'

# 3. No System.Runtime.Numerics (BigInteger library not pulled in)
riscv64-linux-gnu-nm zisk/output/nethereum_evm_elf | grep System_Runtime_Numerics

# 4. Signal functions patched (first instruction: ret / 00008067)
riscv64-linux-gnu-objdump -d zisk/output/nethereum_evm_elf | grep -A1 "<__block_app_sigs>:"

# 5. ROM fits
cargo-zisk stats -e zisk/output/nethereum_evm_elf -l -n 2>&1 | grep exceed

# 6. Emulator runs
ziskemu -e zisk/output/nethereum_evm_elf -n 10000000
```

`analyze-elf.sh` and `analyze-sections.sh` wrap a few of these in convenience form.

## Proving Notes

- Use the **standard input format** for `cargo-zisk prove` (`-i`). Convert legacy witness files with `cargo-zisk convert-input`. `Nethereum.Zisk.Core.ZiskInput.Read()` auto-detects both formats, so the same binary works with `ziskemu --legacy-inputs` and `cargo-zisk prove -i`.
- **WSL2 users**: Zisk's prover needs ≥28 GB allocated to the WSL VM. Add to `%USERPROFILE%\.wslconfig`:
  ```ini
  [wsl2]
  memory=28GB
  swap=8GB
  ```
  Then `wsl --shutdown` to apply.
- `cargo-zisk prove` flags:

  | Flag | Purpose |
  |------|---------|
  | `-e` | ELF path |
  | `-i` | Standard-format input file |
  | `-o` | Output directory for proof files |
  | `-l` | Emulator mode (no ASM microservices) |
  | `-n` | No auto ROM setup |
  | `-m` | Minimal memory mode |
  | `-y` | Verify after generation |

## Related Source Projects

| Project | Role |
|---------|------|
| `src/Nethereum.EVM.Core` | Portable sync-capable EVM engine (no platform deps). |
| `src/Nethereum.EVM.Zisk` | Zisk guest bridge: precompile backends + witness reader. |
| `src/Nethereum.Zisk.Core` | Low-level Zisk runtime bindings: I/O, crypto, memory map. |
