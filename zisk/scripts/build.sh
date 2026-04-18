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
# Requires: Docker image `nethereum/bflat-riscv64` built from the Nethereum/bflat-riscv64 fork's
#           `nethereum` branch (which already bakes the TSS reduction and calloc fix into rhp.o,
#           plus the signal_patch and patch_elf tools). Build once with:
#               git clone -b nethereum https://github.com/Nethereum/bflat-riscv64.git
#               cd bflat-riscv64 && docker build -t nethereum/bflat-riscv64 .
#
# Override the image:          ZISK_IMAGE=<image-name>
# Use a host-built libziskos:  LIBZISKOS_DIR=<host-path>   (mounted at /libziskos)
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NETHEREUM="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT_DIR="zisk/output"
IMAGE="${ZISK_IMAGE:-nethereum/bflat-riscv64}"

EXTLIB_FLAG=""
LIBZISKOS_MOUNT=""
if [ -n "${LIBZISKOS_DIR:-}" ] && [ -f "$LIBZISKOS_DIR/libziskos.bflat.manifest" ]; then
    EXTLIB_FLAG="--extlib /libziskos/libziskos.bflat.manifest"
    LIBZISKOS_MOUNT="-v $LIBZISKOS_DIR:/libziskos"
    echo "Using host libziskos: $LIBZISKOS_DIR"
fi
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
  MSYS_NO_PATHCONV=1 docker run --rm -v "$NETHEREUM:/src" $LIBZISKOS_MOUNT "$IMAGE" bash -c '
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

MSYS_NO_PATHCONV=1 docker run --rm -v "$NETHEREUM:/src" $LIBZISKOS_MOUNT "$IMAGE" bash -c '
set -e
SRC=""

# Zisk.Core
for f in $(find /src/src/Nethereum.Zisk.Core -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" 2>/dev/null); do SRC="$SRC $f"; done

# EVM-Zisk bridge
for f in /src/src/Nethereum.EVM.Zisk/Zisk/*.cs; do [ -f "$f" ] && SRC="$SRC $f"; done
for f in $(find /src/src/Nethereum.EVM.Zisk/Zisk/Backends -name "*.cs" 2>/dev/null); do SRC="$SRC $f"; done

# Util (minimal — no BigDecimal, no LINQ)
for util in EvmUInt256 EvmInt256 AddressUtil AddressExtensions ContractUtils Sha3Keccack ByteUtil EvmUInt256RLPExtensions; do
    [ -f "/src/src/Nethereum.Util/${util}.cs" ] && SRC="$SRC /src/src/Nethereum.Util/${util}.cs"
done
SRC="$SRC /src/src/Nethereum.Util/Keccak/KeccakDigest.cs"
SRC="$SRC /src/src/Nethereum.Util/HashProviders/IHashProvider.cs"
SRC="$SRC /src/src/Nethereum.Util/HashProviders/Sha3KeccackHashProvider.cs"
SRC="$SRC /src/src/Nethereum.Util/HashProviders/Sha256HashProvider.cs"
SRC="$SRC /src/src/Nethereum.Util/HashProviders/PoseidonPairHashProvider.cs"
SRC="$SRC /src/src/Nethereum.Util/HashProviders/PoseidonHashProvider.cs"
SRC="$SRC /src/src/Nethereum.Util/Poseidon/IPoseidonFieldOps.cs"
SRC="$SRC /src/src/Nethereum.Util/Poseidon/PoseidonCore.cs"
SRC="$SRC /src/src/Nethereum.Util/Poseidon/EvmUInt256PoseidonField.cs"
SRC="$SRC /src/src/Nethereum.Util/PoseidonEvmHasher.cs"
SRC="$SRC /src/src/Nethereum.Util/PoseidonPrecomputedConstants.cs"
SRC="$SRC /src/src/Nethereum.Util/PoseidonPrecomputedPresets.cs"
SRC="$SRC /src/src/Nethereum.Util/PoseidonParameterPreset.cs"

# Merkle Binary (EIP-7864 binary trie — all source)
for f in $(find /src/src/Nethereum.Merkle.Binary -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -not -name "PLAN.md" 2>/dev/null); do SRC="$SRC $f"; done

# Hex
SRC="$SRC /src/src/Nethereum.Hex/HexConvertors/Extensions/HexByteConvertorExtensions.cs"

# EVM.Core (all)
for f in $(find /src/src/Nethereum.EVM.Core -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*"); do SRC="$SRC $f"; done

# Merkle Patricia (no proof verification)
for f in $(find /src/src/Nethereum.Merkle.Patricia -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -not -name "*ProofVerification*.cs" 2>/dev/null); do SRC="$SRC $f"; done

# CoreChain (state root calculators)
for cc in PatriciaStateRootCalculator PatriciaMerkleTreeBuilder PatriciaBlockRootCalculator BinaryStateRootCalculator; do
    [ -f "/src/src/Nethereum.CoreChain/${cc}.cs" ] && SRC="$SRC /src/src/Nethereum.CoreChain/${cc}.cs"
done

# Model types
for model in \
    AccessListItem IBlockEncodingProvider RlpBlockEncodingProvider \
    Authorisation7702Signed Signature ISignature DefaultValues \
    Account AccountEncoder Receipt ReceiptEncoder Log LogEncoder LogBloomFilter \
    BlockHeader BlockHeaderEncoder \
    TransactionFactory TransactionType TransactionTypeEncoder \
    ISignedTransaction ITransactionTypeDecoder \
    SignedTransaction SignedTypeTransaction SignedLegacyTransaction \
    SignedTransactionBase SignedTransactionExtensions SignatureExtensions \
    VRecoveryAndChainCalculations \
    LegacyTransaction LegacyTransactionChainId \
    Transaction1559 Transaction1559Encoder \
    Transaction2930 Transaction2930Encoder \
    Transaction7702 Transaction7702Encoder \
    RLPSignedDataHashBuilder RLPSignedDataDecoder SignedData \
    AccessListRLPEncoderDecoder AuthorisationListRLPEncoderDecoder \
    Authorisation7702RLPEncoderAndHasher RLPSignedDataEncoder
do
    [ -f "/src/src/Nethereum.Model/${model}.cs" ] && SRC="$SRC /src/src/Nethereum.Model/${model}.cs"
done

# RLP (all)
for f in $(find /src/src/Nethereum.RLP -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -not -path "*/Properties/*" 2>/dev/null); do SRC="$SRC $f"; done

# Assemble Nethereum syscalls (poseidon2 etc. — separate from libziskos to avoid DMA symbols)
NETHEREUM_EXTLIB=""
if [ -f /src/zisk/nethereum_syscalls.S ]; then
    riscv64-linux-gnu-as --march=rv64ima --mabi=lp64 /src/zisk/nethereum_syscalls.S -o /tmp/nethereum_syscalls.o
    riscv64-linux-gnu-gcc -c -march=rv64ima -mabi=lp64 -O2 -fno-builtin /src/zisk/nethereum_poseidon2.c -o /tmp/nethereum_poseidon2.o
    riscv64-linux-gnu-ar rcs /tmp/libnethereum_precompiles.a /tmp/nethereum_syscalls.o /tmp/nethereum_poseidon2.o
    cat > /tmp/nethereum_precompiles.bflat.manifest << MANIFEST
{
  "name": "nethereum_precompiles",
  "package_version": "1.0.0",
  "builds": [{ "arch": "riscv64", "os": "linux", "libc": "zisk", "static_lib": "/tmp/libnethereum_precompiles.a" }]
}
MANIFEST
    NETHEREUM_EXTLIB="--extlib /tmp/nethereum_precompiles.bflat.manifest"
    echo "[BUILD] Assembled nethereum_precompiles.a"
fi

echo "[BUILD] Compiling $(echo $SRC | wc -w) source files..."

bflat build $SRC \
    --os linux --arch riscv64 --libc zisk \
    --no-globalization --no-pthread --no-stacktrace-data \
    --no-exception-messages \
    -Os --no-pie \
    -d EVM_SYNC \
    '"$EXTLIB_FLAG"' $NETHEREUM_EXTLIB \
    -o /src/'"$OUTPUT_DIR"'/'"$OUTPUT_NAME"'_raw
'

fi  # end MODE branch

# --- Step 2: Post-process ELF ---
echo ""
echo "=== Step 2: Post-process ELF ==="

MSYS_NO_PATHCONV=1 docker run --rm -v "$NETHEREUM:/src" "$IMAGE" \
    patch_elf "/src/$OUTPUT_DIR/${OUTPUT_NAME}_raw" "/src/$OUTPUT_DIR/${OUTPUT_NAME}_elf" \
    --fix-init-array --fix-tdata --remove-eh --split-code-data

# --- Step 3: Signal patching ---
echo ""
echo "=== Step 3: Signal patching ==="

MSYS_NO_PATHCONV=1 docker run --rm -v "$NETHEREUM:/src" "$IMAGE" \
    signal_patch "/src/$OUTPUT_DIR/${OUTPUT_NAME}_elf"

echo ""
ls -lh "$NETHEREUM/$OUTPUT_DIR/${OUTPUT_NAME}_elf"
echo ""
echo "============================================="
echo "  BUILD COMPLETE: $OUTPUT_DIR/${OUTPUT_NAME}_elf"
echo "============================================="
# Convert Windows path to WSL path for convenience
WSL_PATH=$(echo "$NETHEREUM" | sed 's|^/c/|/mnt/c/|;s|^C:/|/mnt/c/|;s|^/\([a-zA-Z]\)/|/mnt/\L\1/|')
echo ""
echo "Run in emulator:"
echo "  wsl -d Ubuntu -- ~/.zisk/bin/ziskemu -e ${WSL_PATH}/$OUTPUT_DIR/${OUTPUT_NAME}_elf --legacy-inputs <witness.bin> -n 500000000"
echo ""
echo "Generate proof:"
echo "  wsl -d Ubuntu -- ~/.zisk/bin/cargo-zisk prove -e ${WSL_PATH}/$OUTPUT_DIR/${OUTPUT_NAME}_elf -i <witness.bin> -l -m"
