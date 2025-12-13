#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BLS_DIR="$ROOT_DIR/external/bls"
OUT_DIR="$ROOT_DIR/src/Nethereum.Signer.Bls.Herumi/runtimes"

if [ ! -d "$BLS_DIR" ]; then
  echo "Herumi BLS source not found at $BLS_DIR. Run 'git submodule update --init --recursive' first." >&2
  exit 1
fi

pushd "$BLS_DIR" >/dev/null
git submodule update --init --recursive

# Build Herumi's ETH preset shared library (Linux as baseline, other platforms can be added later).
make ETH=1
popd >/dev/null

mkdir -p "$OUT_DIR/linux-x64/native"
cp "$BLS_DIR/lib/lib.so" "$OUT_DIR/linux-x64/native/libbls_eth.so" || {
  echo "Expected lib.so not found. Check the Herumi build output." >&2
  exit 1
}

# Placeholder for macOS; uncomment once you build the dylib locally.
# mkdir -p "$OUT_DIR/osx-x64/native"
# cp "$BLS_DIR/bls/bin/libbls384_256.dylib" "$OUT_DIR/osx-x64/native/libbls_eth.dylib"

echo "Herumi BLS artifacts copied into $OUT_DIR"
