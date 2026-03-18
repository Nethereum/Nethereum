#!/bin/bash
# Generate C# contract services from Forge compiled output
# Usage: ./scripts/generate-csharp.sh [-b]
#   -b  Build contracts with Forge before generating

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

CONFIG_FILE="$PROJECT_ROOT/contracts/.nethereum-gen.multisettings"

if [ "$1" = "-b" ] || [ "$1" = "--build" ]; then
    echo "Building contracts with Forge..."
    cd "$PROJECT_ROOT/contracts"
    forge build
    cd "$PROJECT_ROOT"
    echo ""
fi

if [ ! -f "$CONFIG_FILE" ]; then
    echo "ERROR: Config file not found: $CONFIG_FILE"
    echo "Make sure you are running this from the project root."
    exit 1
fi

echo "=== Generating C# Contract Services ==="
echo "Config: $CONFIG_FILE"
echo ""

dotnet run --project generators/Nethereum.Generator.Console/Nethereum.Generator.Console.csproj \
    -- generate from-config \
    -cfg "$CONFIG_FILE" \
    -r "$PROJECT_ROOT/contracts"

echo ""
echo "=== Generation Complete ==="
echo "Generated files are in ContractServices/"
