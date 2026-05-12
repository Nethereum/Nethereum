#!/bin/bash
# =============================================================================
# Build libziskos.a from Zisk upstream inside Docker
#
# Everything runs inside a rust:latest container — no host Rust needed.
# Works on Windows (MSYS/Git Bash) and Linux.
#
# Usage: bash zisk/scripts/build-libziskos.sh [zisk-git-ref]
#   Default ref: main
#
# Output: zisk/.libziskos/libziskos.a + manifest
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NETHEREUM="$(cd "$SCRIPT_DIR/../.." && pwd)"
ZISK_REF="${1:-main}"
ZISK_COMMIT_KNOWN="48cf7ccefb5ed62261abf6bfb007b5be8a23c547"
OUTPUT_DIR="$NETHEREUM/zisk/.libziskos"

echo "============================================="
echo "  Build libziskos.a from Zisk upstream"
echo "  Ref: $ZISK_REF"
echo "============================================="

mkdir -p "$OUTPUT_DIR/runtimes/linux-riscv64/native"

MSYS_NO_PATHCONV=1 docker run --rm \
    -v "$OUTPUT_DIR:/output" \
    -e "ZISK_REF=$ZISK_REF" \
    rust:latest bash -c '
set -e
apt-get update -qq && apt-get install -y -qq gcc-riscv64-linux-gnu binutils-riscv64-linux-gnu python3 libclang-dev > /dev/null 2>&1
rustup toolchain install nightly
rustup component add rust-src --toolchain nightly

echo "=== Cloning Zisk ($ZISK_REF) ==="
cd /tmp
git clone --depth 1 --branch "$ZISK_REF" https://github.com/0xPolygonHermez/zisk.git 2>&1 | tail -3
cd /tmp/zisk
ZISK_COMMIT=$(git rev-parse HEAD)
echo "Commit: $ZISK_COMMIT"

echo "=== Applying patches ==="

# Patch 1: Add staticlib crate type (skip if already present)
if ! grep -q "^\[lib\]" ziskos/entrypoint/Cargo.toml; then
    sed -i "/^\[dependencies\]/i [lib]\ncrate-type = [\"staticlib\", \"rlib\"]\n" ziskos/entrypoint/Cargo.toml
    echo "[lib] section added"
else
    echo "[lib] section already present, skipping"
fi

# Patch 2: Add no_entrypoint feature
sed -i "/^\[features\]/a no_entrypoint = []" ziskos/entrypoint/Cargo.toml

# Patch 3: Guard _start and _zisk_main with no_entrypoint
LINE_START=$(grep -n "fn _start" ziskos/entrypoint/src/lib.rs | head -1 | cut -d: -f1)
LINE_MAIN=$(grep -n "fn _zisk_main" ziskos/entrypoint/src/lib.rs | head -1 | cut -d: -f1)
if [ -n "$LINE_MAIN" ]; then
    sed -i "${LINE_MAIN}i\\    #[cfg(not(feature = \"no_entrypoint\"))]" ziskos/entrypoint/src/lib.rs
fi
if [ -n "$LINE_START" ]; then
    sed -i "${LINE_START}i\\    #[cfg(not(feature = \"no_entrypoint\"))]" ziskos/entrypoint/src/lib.rs
fi
echo "Guarded _start (line $LINE_START) and _zisk_main (line $LINE_MAIN)"

# Patch 4: Add sys_panic stub (insert before the closing of the ziskos module)
# Find last } in lib.rs and insert before it
head -n -1 ziskos/entrypoint/src/lib.rs > /tmp/lib_rs_tmp
echo "" >> /tmp/lib_rs_tmp
echo "    #[no_mangle]" >> /tmp/lib_rs_tmp
echo "    extern \"C\" fn sys_panic(_msg_ptr: *const u8, _msg_len: usize) -> ! {" >> /tmp/lib_rs_tmp
echo "        loop {}" >> /tmp/lib_rs_tmp
echo "    }" >> /tmp/lib_rs_tmp
echo "}" >> /tmp/lib_rs_tmp
mv /tmp/lib_rs_tmp ziskos/entrypoint/src/lib.rs

# Patch 5: Replace bump allocator with malloc wrapper
ALLOC_FILE="ziskos/entrypoint/src/alloc/alloc.rs"
# Write replacement function to temp file, then use awk to swap
cat > /tmp/alloc_new.txt << "ALLOCEOF"
#[inline(never)]
pub unsafe fn inline_bump_alloc_aligned(bytes: usize, align: usize) -> *mut u8 {
    extern "C" {
        fn malloc(n: core::ffi::c_ulong) -> *mut core::ffi::c_void;
    }
    let extra = if align > 1 { align - 1 } else { 0 };
    let raw = unsafe { malloc((bytes + extra) as core::ffi::c_ulong) as usize };
    let aligned = (raw + extra) & !(align - 1);
    aligned as *mut u8
}
ALLOCEOF
# Use sed to replace the function: delete from #[inline(always)] to closing }, insert new
# First, get line numbers
START=$(grep -n "inline.always" "$ALLOC_FILE" | head -1 | cut -d: -f1)
# Find the closing } of the function (standalone } on a line after the fn signature)
END=$(tail -n +$START "$ALLOC_FILE" | grep -n "^}" | head -1 | cut -d: -f1)
END=$((START + END - 1))
# Delete old, insert new
head -n $((START-1)) "$ALLOC_FILE" > "${ALLOC_FILE}.tmp"
cat /tmp/alloc_new.txt >> "${ALLOC_FILE}.tmp"
tail -n +$((END+1)) "$ALLOC_FILE" >> "${ALLOC_FILE}.tmp"
mv "${ALLOC_FILE}.tmp" "$ALLOC_FILE"
echo "alloc.rs patched (lines $START-$END replaced)"

# Patch 6: Wrap DMA memcpy/memset/memmove/memcmp symbols
for f in memcpy memset memmove memcmp; do
    file="ziskos/entrypoint/src/dma/${f}.s"
    if [ -f "$file" ]; then
        sed -i "s/${f}/__wrap_${f}/g" "$file"
    fi
done
echo "DMA symbols wrapped"

# Patch 7: Export pairing_check functions so we can call them directly from C#.
# The zkvm_* wrappers crash because inner functions use Vec (heap alloc).
# Bypassing the wrapper and calling the _check_c function directly works.
cat > /tmp/patch_pairing.py << "PATCHEOF"
for path in ["ziskos/entrypoint/src/zisklib/lib/bn254/pairing.rs", "ziskos/entrypoint/src/zisklib/lib/bls12_381/pairing.rs"]:
    try:
        txt = open(path).read()
    except FileNotFoundError:
        continue
    txt = txt.replace("pub(crate) unsafe fn bn254_pairing_check_c(", "#[no_mangle]\npub unsafe extern \"C\" fn bn254_pairing_check_c(")
    txt = txt.replace("pub(crate) unsafe fn bls12_381_pairing_check_c(", "#[no_mangle]\npub unsafe extern \"C\" fn bls12_381_pairing_check_c(")
    open(path, "w").write(txt)
    print(path + ": pairing_check_c exported")
PATCHEOF
python3 /tmp/patch_pairing.py

# Patch 8: Skip lib-c native build for zkvm target
sed -i "3a\\use std::env;" lib-c/build.rs 2>/dev/null || true
sed -i "/^fn main/a\\    if env::var(\"CARGO_CFG_TARGET_OS\").unwrap_or_default() == \"zkvm\" { println!(\"cargo:rustc-cfg=feature=\\\\\"no_lib_link\\\\\"\"); return; }" lib-c/build.rs 2>/dev/null || true

# Create target spec
cat > riscv64imad-zisk-zkvm-elf.json << "SPEC"
{
  "llvm-target": "riscv64",
  "llvm-abiname": "lp64d",
  "data-layout": "e-m:e-p:64:64-i64:64-i128:128-n32:64-S128",
  "arch": "riscv64",
  "target-endian": "little",
  "target-pointer-width": 64,
  "target-c-int-width": 32,
  "os": "zkvm",
  "vendor": "zisk",
  "env": "",
  "linker-flavor": "ld.lld",
  "linker": "rust-lld",
  "cpu": "generic-rv64",
  "features": "+m,+a,+d",
  "max-atomic-width": 64,
  "atomic-cas": true,
  "executables": true,
  "panic-strategy": "abort",
  "relocation-model": "static",
  "code-model": "medium",
  "emit-debug-gdb-scripts": false,
  "eh-frame-header": false,
  "disable-redzone": true
}
SPEC

echo "=== Building Rust crate ==="
cd ziskos/entrypoint
cargo +nightly build --release \
    --target /tmp/zisk/riscv64imad-zisk-zkvm-elf.json \
    -Z build-std=std,panic_abort \
    -Z json-target-spec \
    --features no_entrypoint 2>&1 | tail -10

BUILT="/tmp/zisk/target/riscv64imad-zisk-zkvm-elf/release/libziskos.a"
if [ ! -f "$BUILT" ]; then
    echo "ERROR: libziskos.a not found"
    find /tmp/zisk/target -name "*.a" -type f 2>/dev/null
    exit 1
fi

echo "=== Adding poseidon2_c alias ==="
cat > /tmp/extras.c << "CEOF"
extern void syscall_poseidon2(unsigned long *state);
void poseidon2_c(unsigned long *state) { syscall_poseidon2(state); }
CEOF
riscv64-linux-gnu-gcc -c -march=rv64ima -mabi=lp64 -O2 -fno-builtin /tmp/extras.c -o /tmp/extras.o
riscv64-linux-gnu-ar r "$BUILT" /tmp/extras.o
riscv64-linux-gnu-ranlib "$BUILT"

echo "=== Copying output ==="
cp "$BUILT" /output/libziskos.a
cp "$BUILT" /output/runtimes/linux-riscv64/native/libziskos.a

cat > /output/libziskos.bflat.manifest << MEOF
{
  "name": "libziskos",
  "package_version": "1.0.0",
  "zisk_ref": "$ZISK_REF",
  "zisk_commit": "$ZISK_COMMIT",
  "source": "Zisk upstream + Nethereum poseidon2_c",
  "builds": [{
    "arch": "riscv64",
    "os": "linux",
    "libc": "zisk",
    "static_lib": "runtimes/linux-riscv64/native/libziskos.a"
  }],
  "wrap_symbols": ["memcpy", "memset", "memmove", "memcmp"]
}
MEOF

echo "=== Verifying symbols ==="
riscv64-linux-gnu-nm /output/libziskos.a | grep " T " | grep -E "keccak256_c|sha256_c|poseidon2_c|bn254_pairing|secp256r1_ecdsa|blake2b_compress" | sort

SIZE=$(du -h /output/libziskos.a | cut -f1)
echo ""
echo "BUILD COMPLETE: $SIZE"
echo "Zisk: $ZISK_REF ($ZISK_COMMIT)"
'

echo ""
echo "============================================="
echo "  Output: zisk/.libziskos/libziskos.a"
echo "  Size: $(du -h "$OUTPUT_DIR/libziskos.a" | cut -f1)"
echo "============================================="
