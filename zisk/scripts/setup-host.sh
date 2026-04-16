#!/bin/bash
set -e

echo "=== Step 1: Install system dependencies ==="
sudo apt-get update -qq
sudo apt-get install -y libomp5-14 libomp-dev gcc-riscv64-linux-gnu libicu-dev
echo "DONE: system deps"

# Create libomp.so.5 symlink if needed
if [ ! -f /usr/lib/x86_64-linux-gnu/libomp.so.5 ]; then
    LIBOMP=$(find /usr -name 'libomp.so.5*' 2>/dev/null | head -1)
    if [ -n "$LIBOMP" ]; then
        sudo ln -sf "$LIBOMP" /usr/lib/x86_64-linux-gnu/libomp.so.5
        echo "Created libomp.so.5 symlink"
    fi
fi

echo "=== Step 2: Install .NET 10 SDK ==="
if ! dotnet --list-sdks | grep -q '^10\.'; then
    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    sudo bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/lib/dotnet
    echo "DONE: .NET 10 installed"
else
    echo "SKIP: .NET 10 already installed"
fi
dotnet --list-sdks

echo "=== Step 3: Install Rust (if needed) ==="
if ! command -v rustup &>/dev/null; then
    curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
fi
source "$HOME/.cargo/env"
rustc --version

echo "=== Step 4: Install Zisk ==="
mkdir -p ~/.zisk/bin
if [ ! -f ~/.zisk/bin/ziskup ]; then
    curl -sSL https://raw.githubusercontent.com/0xPolygonHermez/zisk/main/ziskup/ziskup -o ~/.zisk/bin/ziskup
    chmod +x ~/.zisk/bin/ziskup
fi
export PATH="$HOME/.zisk/bin:$PATH"
~/.zisk/bin/ziskup --nokey
echo "DONE: Zisk installed"

echo "=== Step 5: Verify installations ==="
echo "--- .NET SDKs ---"
dotnet --list-sdks
echo "--- Rust ---"
rustc --version
echo "--- Zisk ---"
cargo-zisk --version 2>&1 || echo "cargo-zisk not yet functional"
echo "--- RISC-V GCC ---"
riscv64-linux-gnu-gcc --version 2>&1 | head -1 || echo "riscv64 gcc not installed"

echo "=== Step 6: Clone/update tools ==="
mkdir -p ~/tools
if [ ! -d ~/tools/bflat-riscv64 ]; then
    git clone --branch nethereum --depth 1 https://github.com/Nethereum/bflat-riscv64.git ~/tools/bflat-riscv64
fi
if [ ! -d ~/tools/bflat-libziskos ]; then
    git clone --depth 1 https://github.com/NethermindEth/bflat-libziskos.git ~/tools/bflat-libziskos
fi
echo "DONE: tools cloned"

echo ""
echo "=== Step 7: Build the Docker image ==="
if command -v docker &>/dev/null; then
    if ! docker image inspect nethereum/bflat-riscv64:latest &>/dev/null; then
        (cd ~/tools/bflat-riscv64 && docker build -t nethereum/bflat-riscv64 .)
        echo "DONE: nethereum/bflat-riscv64 image built"
    else
        echo "SKIP: nethereum/bflat-riscv64 image already present"
    fi
else
    echo "SKIP: docker not installed — build the image manually from ~/tools/bflat-riscv64"
fi

echo ""
echo "========================================="
echo "  Setup complete!"
echo "========================================="
