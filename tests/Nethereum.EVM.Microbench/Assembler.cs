using System.Numerics;

namespace Nethereum.EVM.Microbench
{
    public sealed class Assembler
    {
        private readonly List<byte> _code = new List<byte>();

        public int Length => _code.Count;

        public Assembler Op(params byte[] opcodes)
        {
            _code.AddRange(opcodes);
            return this;
        }

        public Assembler Push(BigInteger value)
        {
            var raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (raw.Length == 0) raw = new byte[] { 0 };
            _code.Add((byte)(0x5f + raw.Length));
            _code.AddRange(raw);
            return this;
        }

        public Assembler Push32(BigInteger value)
        {
            var raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            _code.Add(0x7f);
            _code.AddRange(new byte[32 - raw.Length]);
            _code.AddRange(raw);
            return this;
        }

        public Assembler PushAddress(byte[] address)
        {
            _code.Add(0x73);
            _code.AddRange(address);
            return this;
        }

        public Assembler Dup(int n) => Op((byte)(0x7f + n));

        public Assembler Swap(int n) => Op((byte)(0x8f + n));

        public Assembler Push2(int value) => Op(0x61, (byte)(value >> 8), (byte)value);

        public Assembler Append(byte[] bytes)
        {
            _code.AddRange(bytes);
            return this;
        }

        public byte[] PaddedTo(int size, byte padding)
        {
            var bytes = _code.ToArray();
            if (bytes.Length >= size) return bytes;
            var padded = new byte[size];
            Array.Copy(bytes, padded, bytes.Length);
            for (int i = bytes.Length; i < size; i++) padded[i] = padding;
            return padded;
        }

        public byte[] ToArray() => _code.ToArray();
    }

    public static class Opcodes
    {
        public const byte Stop = 0x00, Add = 0x01, Mul = 0x02, Sub = 0x03, Div = 0x04, SDiv = 0x05, Mod = 0x06, SMod = 0x07,
            AddMod = 0x08, MulMod = 0x09, Exp = 0x0a, SignExtend = 0x0b, Keccak = 0x20, Gas = 0x5a,
            CalldataSize = 0x36, CalldataCopy = 0x37, CodeSize = 0x38, CodeCopy = 0x39, ExtCodeCopy = 0x3c,
            ReturnDataSize = 0x3d, ReturnDataCopy = 0x3e, MLoad = 0x51, MStore = 0x52, MStore8 = 0x53,
            Lt = 0x10, IsZero = 0x15, CallValue = 0x34, TLoad = 0x5c, TStore = 0x5d, Jump = 0x56, JumpI = 0x57, JumpDest = 0x5b, MCopy = 0x5e, Push0 = 0x5f, Pop = 0x50, Swap1 = 0x90,
            Log0 = 0xa0, Call = 0xf1, Return = 0xf3, StaticCall = 0xfa, Create = 0xf0, Create2 = 0xf5, Revert = 0xfd, Invalid = 0xfe, CallDataLoad = 0x35;
    }
}
