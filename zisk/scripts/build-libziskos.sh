#!/bin/bash
# =============================================================================
# Build libziskos.a for the Nethereum Zisk guest from the Zisk release tag.
#
# Usage: bash zisk/scripts/build-libziskos.sh [zisk-git-ref]
#   Default ref: v1.3.1-alpha
#
# Needs rustup, the Zisk toolchain (`cargo-zisk toolchain install`), git and make.
# Zisk builds the isolated staticlib itself (`make libziskos`): its own `_start`
# calls `main`, its allocator is private to the library and every accelerator is
# exported as `zkvm_*`. The only change made here is renaming the DMA memory
# routines to `__wrap_*`, which bflat links in place of musl's memcpy, memset,
# memmove and memcmp through the manifest's wrap_symbols.
#
# Output: zisk/.libziskos/libziskos.a + libziskos.bflat.manifest
#         (override the directory with LIBZISKOS_OUT)
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NETHEREUM="$(cd "$SCRIPT_DIR/../.." && pwd)"
ZISK_REF="${1:-v1.3.1-alpha}"
OUTPUT_DIR="${LIBZISKOS_OUT:-$NETHEREUM/zisk/.libziskos}"
WORK_DIR="${ZISK_WORK_DIR:-$(mktemp -d)}"

. "$HOME/.cargo/env" 2>/dev/null || true

if [ ! -d "$WORK_DIR/zisk" ]; then
    git clone --depth 1 --branch "$ZISK_REF" https://github.com/0xPolygonHermez/zisk.git "$WORK_DIR/zisk"
fi
ZISK_COMMIT="$(git -C "$WORK_DIR/zisk" rev-parse HEAD)"

(cd "$WORK_DIR/zisk" && make libziskos)

BUILT="$WORK_DIR/zisk/target/riscv64ima-zisk-zkvm-elf/release/libziskos_staticlib.a"
[ -f "$BUILT" ] || { echo "libziskos_staticlib.a was not built" >&2; exit 1; }

SYSROOT="$(rustup run zisk rustc --print sysroot)"
LLVM_AR="$(find "$SYSROOT" -name llvm-ar -type f | head -1)"
LLVM_OBJCOPY="$(find "$SYSROOT" -name llvm-objcopy -type f | head -1)"

EXTRACT_DIR="$(mktemp -d)"
(cd "$EXTRACT_DIR" && "$LLVM_AR" x "$BUILT")
for object in "$EXTRACT_DIR"/*.o; do
    "$LLVM_OBJCOPY" \
        --redefine-sym memcpy=__wrap_memcpy \
        --redefine-sym memset=__wrap_memset \
        --redefine-sym memmove=__wrap_memmove \
        --redefine-sym memcmp=__wrap_memcmp \
        "$object"
done

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/runtimes/linux-riscv64/native"
"$LLVM_AR" rcs "$OUTPUT_DIR/runtimes/linux-riscv64/native/libziskos.a" "$EXTRACT_DIR"/*.o
cp "$OUTPUT_DIR/runtimes/linux-riscv64/native/libziskos.a" "$OUTPUT_DIR/libziskos.a"
rm -rf "$EXTRACT_DIR"

cat > "$OUTPUT_DIR/libziskos.bflat.manifest" << MEOF
{
  "name": "libziskos",
  "package_version": "1.0.0",
  "zisk_ref": "$ZISK_REF",
  "zisk_commit": "$ZISK_COMMIT",
  "source": "Zisk upstream make libziskos",
  "builds": [{
    "arch": "riscv64",
    "os": "linux",
    "libc": "zisk",
    "static_lib": "runtimes/linux-riscv64/native/libziskos.a"
  }],
  "wrap_symbols": ["memcpy", "memset", "memmove", "memcmp"]
}
MEOF

echo "libziskos.a: $(du -h "$OUTPUT_DIR/libziskos.a" | cut -f1) from Zisk $ZISK_REF ($ZISK_COMMIT)"
