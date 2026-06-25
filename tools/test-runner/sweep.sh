#!/usr/bin/env bash
# Snapshot the test DLL + dependencies, run tests against the snapshot.
# Keeps the main bin/ unlocked so the dev can rebuild and queue more sweeps.
set -e
SRC="tests/Nethereum.EVM.UnitTests/bin/Debug/net8.0"
SNAPSHOT_DIR="tmp/test-snapshots/$(date +%Y%m%d-%H%M%S)-${BASHPID}"
mkdir -p "$SNAPSHOT_DIR"
cp -r "$SRC"/* "$SNAPSHOT_DIR/"
DLL="$SNAPSHOT_DIR/Nethereum.EVM.UnitTests.dll"
echo "[snapshot] $SNAPSHOT_DIR"
dotnet vstest "$DLL" --TestCaseFilter:"$1" --Logger:"console;verbosity=minimal"
