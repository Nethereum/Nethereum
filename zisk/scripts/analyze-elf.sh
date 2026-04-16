#!/bin/bash
FILE=/mnt/c/Users/SuperDev/Documents/Repos/Nethereum/scripts/zisk-output/zisk_raw
riscv64-linux-gnu-nm --print-size "$FILE" 2>/dev/null | grep " [tT] " > /tmp/syms.txt

echo "Total text symbols: $(wc -l < /tmp/syms.txt)"
echo ""

echo "=== TypeLoader/Reflection ==="
grep -i "TypeLoader\|Reflection_Execution\|NativeFormat" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== Enum ==="
grep -i "Enum__\|S_P_CoreLib_System_Enum" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== Number formatting ==="
grep -i "Number__\|Dragon\|Grisu\|NumberToString\|NumberFormat\|NumberBuffer" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== GC ==="
grep -i "gc_heap\|_ZN3WKS\|GCHeap" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== Unwind/EH ==="
grep -i "libunwind\|Dwarf\|GcInfo\|_lsda\|unwind" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== Nethereum/EVM ==="
grep -i "Nethereum\|Evm" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== Threading ==="
grep -i "Thread\|pthread\|Monitor\|Lock__" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo "=== musl/libc ==="
grep -i "musl\|__wrap_\|__float\|__int\|vfscanf\|vfprintf\|printf\|malloc\|free\b" /tmp/syms.txt | awk '{s+=strtonum("0x"$2)} END{print s+0, "bytes,", NR, "symbols"}'

echo ""
echo "=== Total text section size ==="
awk '{s+=strtonum("0x"$2)} END{print s, "bytes (", s/1024, "KB)"}' /tmp/syms.txt
