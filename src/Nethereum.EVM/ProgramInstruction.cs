using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM
{
    public class ProgramInstruction
    {
        public int Step { get; set; }
        public Instruction? Instruction { get; set; }
        public byte Value { get; set; }
        public byte[] Arguments { get; set; }

        public string ArgumentsAsHex(bool hexPrefixed = true)
        {
            return Arguments?.ToHex(hexPrefixed);
        }

        public string ValueAsHex(bool hexPrefixed = true)
        {
            return (hexPrefixed ? "0x" : "") + Value.ToString("X2");
        }

        public string StepAsHex(bool hexPrefixed = true)
        {
            return (hexPrefixed ? "0x" : "") + Step.ToString("X4");
        }

        public string ToDisassemblyLine()
        {
            return $"{StepAsHex(false)}   {ValueAsHex(false)}   {Instruction?.ToString()}  {Arguments?.ToHex(true)}";
        }
    }
}