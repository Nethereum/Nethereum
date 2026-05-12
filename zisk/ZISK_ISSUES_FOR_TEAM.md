# Zisk Issues Report — NativeAOT (.NET) Guest Programs

Hi, great work on Zisk!

I have been working on proving Ethereum EVM execution using Nethereum EVM (C# via NativeAOT → RISC-V → Zisk) and discovered several bugs and tooling issues. Some are already fixed in our fork; others need your attention.

Fork: https://github.com/Nethereum/zisk/tree/fix/nativeaot-constraints

### Who is affected

These bugs are **not NativeAOT-specific**. They affect any guest program that:
- Performs unaligned memory access (bugs #1, #2) — common in C, C++, Go, .NET, any language with packed structs or byte-level memory manipulation
- Uses `patch_elf --split-code-data` with LIEF (bug #3) — any non-Rust ELF that needs the overlay
- Has 4-byte-aligned but not 8-byte-aligned 8-byte reads (bug #4) — C/C++ with `__attribute__((packed))`, Go structs, .NET value types
- Produces >4M execution steps (multi-instance issue) — any non-trivial program in any language

Rust programs avoid most of these because the compiler enforces natural alignment and Zisk's own test suite is Rust-only. But any other language targeting Zisk (C, C++, Go, .NET, Zig) will hit them.

---

## Fixed Issues (5 patches in our fork)

### 1. `get_read_value` double shift — `state-machines/mem-common/src/mem_helpers.rs:146`

Cross-word-boundary read values had an extra `>> offset`:

```rust
// BEFORE (wrong):
value |= read_values[1] << (64 - offset) >> offset;
// AFTER (correct):
value |= read_values[1] << (64 - offset);
```

**Impact:** ~104 MemAlign constraint violations for any program with unaligned 8-byte reads crossing a word boundary.

### 2. Read-before-write in MemAlign TwoWrites — `state-machines/mem/src/mem_align_sm.rs`

The TwoWrites path read values from `first_write_row.get_reg(i)` and `second_write_row.get_reg(i)` before those rows were populated.

**Fix:** Compute values directly from `value_first_write` and `value_second_write` using `Self::get_byte()`.

### 3. Overlapping section merge — `core/src/elf_extraction.rs`

When `patch_elf --split-code-data` creates a `.text_overlay`, LIEF pads it past its declared size, overlapping into `.rodata`. The `merge_adjacent_ro_sections` function had no overlap handling.

**Fix:** Truncate first section at overlap point:
```rust
} else if current_end > section.addr {
    let overlap = (current_end - section.addr) as usize;
    let new_len = current.data.len().saturating_sub(overlap);
    current.data.truncate(new_len);
    if !current.data.is_empty() { merged.push(current); }
    current = section;
}
```

**Impact:** 447/451 RomData constraint violations.

### 4. `is_full_aligned` wrong mask — `core/src/mem.rs` (3 locations)

Alignment check used `0x03` (4-byte mask) instead of `0x07` (8-byte mask):

```rust
// BEFORE (wrong):
((addr & 0x03) == 0) && (width == 8)
// AFTER (correct):
((addr & 0x07) == 0) && (width == 8)
```

**Impact:** 18/160 `read_same_addr` Mem SM violations. An 8-byte read at an address like `0xAFFF1B04` passes the 4-byte check but actually crosses a word boundary.

### 5. Bus payload emits hardcoded `bytes=8` for cross-word-boundary sub-word reads/writes — `emulator/src/emu.rs`

In `source_b_mem_reads_consume_databus()`, the `SRC_IND` double_not_aligned paths hardcoded `8` in the bus payload width field instead of using `instruction.ind_width`:

```rust
// BEFORE (wrong) — read path line 847:
let payload = MemHelpers::mem_load(address as u32, ..., 8, [raw_data_1, raw_data_2]);
// AFTER (correct):
let payload = MemHelpers::mem_load(address as u32, ..., instruction.ind_width as u8, [raw_data_1, raw_data_2]);

// BEFORE (wrong) — write path line 1281:
let payload = MemHelpers::mem_write(address as u32, ..., 8, value, [raw_data_1, raw_data_2]);
// AFTER (correct):
let payload = MemHelpers::mem_write(address as u32, ..., instruction.ind_width as u8, value, [raw_data_1, raw_data_2]);
```

**Impact:** Global constraint failure (opid 10 = MEMORY_ID) for ANY program that performs sub-word reads (LW/LH/LB) at addresses where `offset + width > 8` (crosses 8-byte word boundary). The emulator sends `bytes=8` on the bus, but Main PIL emits assumes with the actual width (4, 2, 1). MemAlign processes the operation as width=8 TwoReads, so its bus entries never match Main's sub-word assumes. This was the root cause of the multi-instance global constraint failure reported earlier — it was never a multi-instance issue, but a bus accounting bug that only manifested when programs did enough sub-word unaligned memory access.

**Diagnosis method:** `cargo-zisk verify-constraints -d` reports `opids do not match [10]`. Full debug mode shows unmatched assumes at `bytes=4` and unmatched proves at `bytes=8` with identical addresses and steps.

---

## Fixed Issues (external to Zisk codebase)

### 5. Float library prebuilt binary has bus bug

The prebuilt `lib-float/c/lib/ziskfloat.elf` (110,208 bytes) has a bus accounting bug that causes Global constraint failure for ALL programs.

**Fix:** Rebuild from source: `cd lib-float/c && make` → produces 112,120 bytes with correct bus accounting.

### 6. `__security_cookie` non-deterministic write (Genernal Netstandard Issue)

NativeAOT's `RhInitialize` writes a time-based canary to `.rodata`. This differs between emulator and prover runs → 1 RomData constraint violation.

**Fix:** Binary-patch the `sd a0, 0(s1)` instruction to NOP. Pattern: `addi s1, aX, <cookie_offset>` → `call minipal_lowres_ticks` → `sd a0, 0(s1)` ← NOP this.

---

## Previously Reported as Unsolved: `global_init_mem` multi-instance Global constraint failure

**STATUS: SOLVED** — This was caused by fix #5 above (hardcoded `bytes=8` in cross-word-boundary bus payload). It was never a multi-instance issue or a `global_init_mem` problem. The bus accounting error only manifested when programs performed enough sub-word unaligned memory operations, which correlated with higher step counts that happened to require multiple Main instances.

With all 5 fixes applied, all witness variants prove and verify successfully:

| Witness | Steps | Main instances | Prove time | Verified |
|---------|-------|---------------|------------|----------|
| ETH transfer (no root) | 3.96M | 1 | 225s | ✓ |
| SSTORE (no root) | 5.27M | 2 | 255s | ✓ |
| ETH transfer (patricia) | 6.59M | 2 | 257s | ✓ |
| SSTORE (patricia) | 8.04M | 2 | 255s | ✓ |

---

## Tooling Issues (pil2-proofman-js)

We had to generate our own v0.17.0 provingKey using `pil2-proofman-js` because the official key isn't published yet. We encountered several issues:

1. **Missing std PIL files** in `stark-recurser` npm package — `std_constants.pil`, `goldilocks.pil` etc. not bundled in `circom2pil/pil/`
2. **circom 2.2.2 stack overflow** on Keccakf compressor (9.28M plonk constraints) — fixed with `ulimit -s unlimited`
3. **circom 2.2.3 double-free** in generated C++ witness code — `release_memory_component()` doesn't null pointers after `delete[]`
4. **`customGatesUses` not an array** — circom R1CS format mismatch with `compressor_setup.js`
5. **Row count assertion failures** in `compressor_setup.js` for Keccakf recursive1
6. **`isCompressorNeeded` corrupts temp files** — called before `genRecursiveSetup`, uses same circom output directory
7. **`witnessLibraryGenerationAwait` hangs forever** — vadcop_final_compressed make completes but `proc.on('close')` never fires
8. **PIL compiler OOM at 115GB** without `-f` flag — fixed with `-f` (fixed-to-file) + `-O no-proto-fixed-data`

### Suggestion

A `cargo-zisk generate-proving-key` command that uses the Rust proofman backend (not the JS toolchain) would eliminate all of these issues.

---

## Environment Notes

- **`HWLOC_COMPONENTS=-gl`** must be set on headless servers — OpenMPI's hwloc probes GPU via X11, causing "Authorization required" hang
- **128GB RAM required** for Keccakf compressor circom compilation (peaks at ~103GB)
- **Server:** Ubuntu 26.04, 12 cores, 128GB RAM, 453GB disk

---

## Summary

| Category | Count | Status |
|----------|-------|--------|
| Zisk Rust bugs fixed | 5 | PR-ready in fork |
| External fixes (float lib, cookie NOP) | 2 | Documented |
| JS tooling issues | 8 | Workarounds applied |

All 5 Rust patches are clean, well-tested, and ready for upstream. With these fixes, the Nethereum EVM (C# NativeAOT → RISC-V) proves and verifies successfully in Zisk for all tested witness variants (up to 8M steps, 2 Main instances).

---

## Pending: PR for bflat-libziskos CSR misalignment

**Repo:** `NethermindEth/bflat-libziskos`
**File:** `src/zisk_syscalls/zisk_syscalls.S`

`secp256r1_add` and `secp256r1_dbl` are mapped to 0x815/0x816 (DMA_INPUTCPY/DMA_MEMSET). Should be 0x817/0x818. `blake2b_round` (0x819) is missing entirely. See `ZISK_CSR_ISSUE.md` in the Nethereum repo for full details and the authoritative CSR map.

```diff
-ZKVM_CSR_VOID     zkvm_secp256r1_add,         0x815
-ZKVM_CSR_VOID     zkvm_secp256r1_dbl,         0x816
+ZKVM_CSR_VOID     zkvm_dma_inputcpy,          0x815
+ZKVM_CSR_VOID     zkvm_dma_memset,            0x816
+ZKVM_CSR_VOID     zkvm_secp256r1_add,         0x817
+ZKVM_CSR_VOID     zkvm_secp256r1_dbl,         0x818
+ZKVM_CSR_VOID     zkvm_blake2b_round,         0x819
```

Note: DMA ops (0x815/0x816) are pattern-matched multi-instruction sequences in the transpiler — may need a different macro than `ZKVM_CSR_VOID`. At minimum secp256r1 must move and blake2b must be added.

---

## libziskos 0.17.1: `zkvm_bn254_pairing` crashes on NativeAOT binaries

**Branch:** `pre-develop-0.17.1` (commit `b3b58619`)
**Symptom:** `Mem::write_silent() invalid addr=15=f` during BN254 pairing execution.

`zkvm_bn254_pairing` internally calls `bn254_pairing_check_c` which uses `Vec::with_capacity(num_pairs)`. The heap allocator (`inline_bump_alloc_aligned` wrapped via `__wrap___libc_malloc_impl`) writes to address `0x0F` — outside the valid memory range (`0xa0000000..0xc0000000`).

This is the same class of issue as the old TSS/calloc allocator bug that bflat-riscv64 wraps around. The NativeAOT bump allocator and the Rust libziskos allocator conflict — Rust's `Vec` allocation goes through `malloc` which resolves to the NativeAOT wrapper, but the wrapper's heap pointer is uninitialized or corrupted when called from inside a Zisk guest.

**Affected functions:** Any `zkvm_*` function that heap-allocates internally: `zkvm_bn254_pairing`, `zkvm_bls12_pairing`, and potentially `zkvm_bls12_g1_msm` / `zkvm_bls12_g2_msm`.

**Not affected:** `zkvm_bn254_g1_add`, `zkvm_bn254_g1_mul`, `zkvm_keccak256`, `zkvm_sha256`, `zkvm_modexp`, `zkvm_secp256k1_ecrecover` — these don't heap-allocate.

**Workaround:** None available from C# side. The old libziskos (`bn254_pairing_check_c`) worked because it was `pub(crate)` and didn't go through the `zkvm_*` wrapper — but it's no longer exported.
