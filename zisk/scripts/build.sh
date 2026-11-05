#!/bin/bash
# =============================================================================
# Build Nethereum EVM for Zisk zkVM
#
# Usage: bash zisk/scripts/build.sh [--dll|--source] [output_name]
#
# Modes:
#   --source (default): bflat compiles all .cs files directly
#   --dll             : dotnet build → bflat links pre-built Nethereum.EVM.Zisk.dll
#
# Requires: Docker image `nethereum/bflat-riscv64:zisk-1.3.1` built from the `zisk-1.3.1` branch of
#           the Nethereum/bflat-riscv64 fork (Zisk 1.3.1 memory map, ziskos entry through main,
#           TSS reduction and calloc fix in rhp.o, patch_elf). Build once with:
#               git clone -b zisk-1.3.1 http://192.168.1.52:3002/Nethereum/bflat-riscv64.git
#               cd bflat-riscv64 && docker build -t nethereum/bflat-riscv64:zisk-1.3.1 .
#           and libziskos.a from `bash zisk/scripts/build-libziskos.sh`.
#
# Override the image:          ZISK_IMAGE=<image-name>
# Use another libziskos:       LIBZISKOS_DIR=<host-path>   (mounted at /libziskos; default zisk/.libziskos)
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NETHEREUM="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT_DIR="zisk/output"
IMAGE="${ZISK_IMAGE:-nethereum/bflat-riscv64:zisk-1.3.1}"
LIBZISKOS_DIR="${LIBZISKOS_DIR:-$NETHEREUM/zisk/.libziskos}"

IN_TOOLCHAIN=false
command -v bflat >/dev/null 2>&1 && IN_TOOLCHAIN=true

EXTLIB_FLAG=""
LIBZISKOS_MOUNT=""
if [ -n "${LIBZISKOS_DIR:-}" ] && [ -f "$LIBZISKOS_DIR/libziskos.bflat.manifest" ]; then
    if $IN_TOOLCHAIN; then
        EXTLIB_FLAG="--extlib $LIBZISKOS_DIR/libziskos.bflat.manifest"
    else
        EXTLIB_FLAG="--extlib /libziskos/libziskos.bflat.manifest"
        LIBZISKOS_MOUNT="-v $LIBZISKOS_DIR:/libziskos"
    fi
    echo "Using libziskos: $LIBZISKOS_DIR"
fi

toolchain_run() {
    if $IN_TOOLCHAIN; then
        [ "$NETHEREUM" = "/src" ] || { echo "Inside the toolchain image the repository must be mounted at /src" >&2; exit 1; }
        bash -c "$1"
    else
        MSYS_NO_PATHCONV=1 docker run --rm --cpus="${ZISK_CPUS:-8}" --entrypoint="" -v "$NETHEREUM:/src" $LIBZISKOS_MOUNT "$IMAGE" bash -c "$1"
    fi
}
MODE="source"
OUTPUT_NAME=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dll) MODE="dll"; shift ;;
    --source) MODE="source"; shift ;;
    *) OUTPUT_NAME="$1"; shift ;;
  esac
done
OUTPUT_NAME="${OUTPUT_NAME:-nethereum_evm}"

mkdir -p "$NETHEREUM/$OUTPUT_DIR"

echo "============================================="
echo "  Nethereum EVM → Zisk Build (mode: $MODE)"
echo "============================================="

if [ "$MODE" = "dll" ]; then
  echo ""
  echo "=== Step 1a: dotnet build Nethereum.EVM.Zisk (Release) ==="
  cd "$NETHEREUM"
  dotnet build src/Nethereum.EVM.Zisk -c Release 2>&1 | tail -5

  echo ""
  echo "=== Step 1b: Link DLL → RISC-V64 via bflat ==="
  echo "Note: DLL mode produces ~25% larger ELF than source mode due to cross-assembly"
  echo "boundary (bflat ILC cannot inline/devirt across DLL refs). Use source mode for"
  echo "production builds; DLL mode is kept for faster iteration."
  toolchain_run '
    set -e
    bflat build /src/src/Nethereum.EVM.Zisk/Stub.cs \
      --os linux --arch riscv64 --libc zisk \
      --no-globalization --no-pthread --no-stacktrace-data \
      --no-exception-messages \
      -Os --no-pie \
      -d EVM_SYNC -d EVM_ZISK \
      -r /src/src/Nethereum.EVM.Zisk/bin/Release/net10.0/Nethereum.EVM.Zisk.dll \
      '"$EXTLIB_FLAG"' \
      -o /src/'"$OUTPUT_DIR"'/'"$OUTPUT_NAME"'_raw
    echo "[BUILD] DLL link complete"
  '
else

# --- Step 1: Compile with bflat (source mode) ---
echo ""
echo "=== Step 1: Compile C# → RISC-V64 ==="

toolchain_run '
set -e
SRC=$(cd /src && DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet msbuild src/Nethereum.EVM.Zisk/Nethereum.EVM.Zisk.csproj -getItem:Compile -nologo \
    | python3 -c "import json,sys; print(\" \".join(i[\"FullPath\"] for i in json.load(sys.stdin)[\"Items\"][\"Compile\"]))")

echo "[BUILD] Compiling $(echo $SRC | wc -w) source files..."

bflat build $SRC \
    --os linux --arch riscv64 --libc zisk \
    --no-globalization --no-pthread --no-stacktrace-data \
    --no-exception-messages \
    -Os --no-pie \
    -d EVM_SYNC -d EVM_ZISK \
    '"$EXTLIB_FLAG"' \
    -o /src/'"$OUTPUT_DIR"'/'"$OUTPUT_NAME"'_raw
'

fi  # end MODE branch

# BuildCommand internally runs patch_elf and outputs .patched file
if [ -f "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_raw.patched" ]; then
    mv "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_raw.patched" "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_elf"
else
    cp "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_raw" "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_elf"
fi

echo ""
ls -lh "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_elf"
echo ""
echo "============================================="
echo "  BUILD COMPLETE: $OUTPUT_DIR/${OUTPUT_NAME}_elf"
echo "============================================="
echo ""
echo "Run in emulator:"
echo "  ziskemu -e $OUTPUT_DIR/${OUTPUT_NAME}_elf --legacy-inputs <witness.bin> -n 500000000"
echo ""
echo "Generate proof:"
echo "  cargo-zisk prove -e $OUTPUT_DIR/${OUTPUT_NAME}_elf -i <witness.bin> --gpu"
