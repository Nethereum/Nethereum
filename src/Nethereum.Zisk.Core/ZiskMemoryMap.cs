namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Zisk zkVM memory layout constants.
    ///
    /// Zisk maps the ELF binary into a fixed memory layout:
    ///   0x40000000 - Input data (read-only, memory-mapped from host file)
    ///   0x80000000 - ROM (executable code + read-only data)
    ///   0xA0000000 - RAM (read-write: 4 MB stack, system registers, .data, .bss, heap)
    ///   0xA0400200 - UART output (single byte writes for debug logging)
    ///   0xA0410000 - Output registers (64 x uint32 result slots)
    /// </summary>
    public static class ZiskMemoryMap
    {
        public const ulong InputBase = 0x4000_0000UL;
        public const ulong RomBase = 0x8000_0000UL;
        public const ulong RamBase = 0xA000_0000UL;
        public const ulong UartAddress = 0xA040_0200UL;
        public const ulong OutputBase = 0xA041_0000UL;
        public const int MaxOutputSlots = 64;
    }
}
