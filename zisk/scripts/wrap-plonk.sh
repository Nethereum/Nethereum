#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NETHEREUM="$(cd "$SCRIPT_DIR/../.." && pwd)"
IMAGE="${ZISK_ZISK_IMAGE:-nethereum-zisk}"
ZISK_HOME="${ZISK_HOME:-$HOME/.zisk}"
STARK_PROOF="${1:?usage: wrap-plonk.sh <stark-proof> <plonk-output>}"
PLONK_OUTPUT="${2:?usage: wrap-plonk.sh <stark-proof> <plonk-output>}"

[ -d "$ZISK_HOME/provingKey" ] || { echo "missing $ZISK_HOME/provingKey (ziskup --provingkey)" >&2; exit 1; }
[ -d "$ZISK_HOME/provingKeySnark" ] || { echo "missing $ZISK_HOME/provingKeySnark (ziskup setup_snark)" >&2; exit 1; }

IN_DIR="$(cd "$(dirname "$STARK_PROOF")" && pwd)"
mkdir -p "$(dirname "$PLONK_OUTPUT")"
OUT_DIR="$(cd "$(dirname "$PLONK_OUTPUT")" && pwd)"

docker run --rm --cpus="${ZISK_CPUS:-8}" ${ZISK_GPU:+--gpus all} --user "$(id -u):$(id -g)" -e HOME=/home/ci \
    -v "$ZISK_HOME/provingKey:/opt/zisk/provingKey:ro" \
    -v "$ZISK_HOME/provingKeySnark:/opt/zisk/provingKeySnark" \
    -v "$IN_DIR:/in:ro" -v "$OUT_DIR:/out" "$IMAGE" \
    cargo-zisk wrap -p "/in/$(basename "$STARK_PROOF")" --plonk ${ZISK_GPU:+--gpu} -o "/out/$(basename "$PLONK_OUTPUT")"

docker run --rm --user "$(id -u):$(id -g)" -e HOME=/home/ci -v "$OUT_DIR:/out:ro" "$IMAGE" \
    cargo-zisk verify -p "/out/$(basename "$PLONK_OUTPUT")"
