#!/usr/bin/env bash
set -euo pipefail

ARCH="${1:-x64}"
RAPIDSNARK_TAG="${RAPIDSNARK_TAG:-v0.0.8}"
case "$ARCH" in
  x64) PLATFORM=linux/amd64; GMP_TARGET=host; TARGET_PLATFORM=x86_64 ;;
  arm64) PLATFORM=linux/arm64; GMP_TARGET=aarch64; TARGET_PLATFORM=aarch64 ;;
  *) echo "usage: $0 [x64|arm64]" >&2; exit 2 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/src/Nethereum.ZkProofs.RapidSnark/runtimes/linux-$ARCH/native"
mkdir -p "$OUTPUT_DIR"

docker run --rm --platform "$PLATFORM" -v "$OUTPUT_DIR:/out" ubuntu:20.04 bash -c "
set -euo pipefail
apt-get update -qq
DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends \
  build-essential cmake nasm m4 curl git ca-certificates xz-utils > /dev/null
git clone -q --recursive --branch $RAPIDSNARK_TAG --depth 1 https://github.com/iden3/rapidsnark.git /rapidsnark
cd /rapidsnark
bash build_gmp.sh $GMP_TARGET > /dev/null
mkdir build_prover && cd build_prover
cmake .. -DTARGET_PLATFORM=$TARGET_PLATFORM -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=../package \
  -DCMAKE_SHARED_LINKER_FLAGS='-static-libstdc++ -static-libgcc' > /dev/null
make -j\$(nproc) > /dev/null
make install > /dev/null
cp ../package/lib/librapidsnark.so /out/librapidsnark.so
chown $(id -u):$(id -g) /out/librapidsnark.so
"

objdump -p "$OUTPUT_DIR/librapidsnark.so" | grep NEEDED
strings "$OUTPUT_DIR/librapidsnark.so" | grep -oE "^GLIBC_[0-9.]+" | sort -Vu | tail -1
