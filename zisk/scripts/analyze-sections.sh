#!/bin/bash
ELF=$1
echo "=== Section sizes ==="
riscv64-linux-gnu-readelf -S "$ELF" 2>/dev/null | grep -E "\.text|\.plt|\.rodata|\.eh_frame|\.dotnet_eh|\.rela|\.data|\.bss|\.got" | awk '{
    name=$2; size=$6; flags=$7
    s = strtonum("0x" size)
    printf "  %-22s %8d bytes  (%6.1f KB)  flags=%s\n", name, s, s/1024, flags
}'

echo ""
echo "=== .text section (code) ==="
riscv64-linux-gnu-size -A "$ELF" 2>/dev/null | grep "\.text"

echo ""
echo "=== Instruction count estimate ==="
text_size=$(riscv64-linux-gnu-readelf -S "$ELF" 2>/dev/null | grep "\.text " | awk '{print $6}')
text_bytes=$(python3 -c "print(int('$text_size', 16))")
echo ".text = $text_bytes bytes"
echo "RISC-V instructions (4 bytes each) = $((text_bytes / 4))"
echo "Zisk expands ~6x = $(( (text_bytes / 4) * 6 ))"

echo ""
echo "=== Total loadable size ==="
riscv64-linux-gnu-readelf -l "$ELF" 2>/dev/null | grep LOAD
