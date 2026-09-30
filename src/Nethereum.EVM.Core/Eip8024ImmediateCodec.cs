namespace Nethereum.EVM
{
    public static class Eip8024ImmediateCodec
    {
        /// <summary>
        /// DUPN/SWAPN immediate decode. Valid iff <c>x &lt;= 90 || x &gt;= 128</c>
        /// (i.e. NOT in the forbidden range 91..127 / 0x5B..0x7F — deliberately
        /// including JUMPDEST's 0x5B so it can never be swallowed as a valid
        /// immediate). EIP-8024 DUPN: "Push <c>stack[top - n + 1]</c> on the
        /// stack"; SWAPN: "Swap <c>stack[top - n]</c> and <c>stack[top]</c>".
        /// Decodes 17 &lt;= n &lt;= 235.
        /// </summary>
        public static bool TryDecodeSingle(byte x, out int n)
        {
            if (!(x <= 90 || x >= 128))
            {
                n = 0;
                return false;
            }

            n = (x + 145) % 256;
            return true;
        }

        /// <summary>
        /// EXCHANGE immediate decode. Valid iff <c>x &lt;= 81 || x &gt;= 128</c>
        /// (forbidden range 82..127 / 0x52..0x7F, also covering 0x5B).
        /// EIP-8024 EXCHANGE: "Swap <c>stack[top - n]</c> and
        /// <c>stack[top - m]</c>". Decodes 1 &lt;= n &lt; m &lt;= 29,
        /// n + m &lt;= 30.
        /// </summary>
        public static bool TryDecodePair(byte x, out int n, out int m)
        {
            if (!(x <= 81 || x >= 128))
            {
                n = 0;
                m = 0;
                return false;
            }

            int k = x ^ 143;
            int q = k / 16;
            int r = k % 16;
            if (q < r)
            {
                n = q + 1;
                m = r + 1;
            }
            else
            {
                n = r + 1;
                m = 29 - q;
            }
            return true;
        }

        public static bool IsStackAccessOpcode(byte opcodeByte)
        {
            return opcodeByte == (byte)Instruction.DUPN
                || opcodeByte == (byte)Instruction.SWAPN
                || opcodeByte == (byte)Instruction.EXCHANGE;
        }

        public static bool IsImmediateValid(byte opcodeByte, byte immediateByte)
        {
            if (opcodeByte == (byte)Instruction.EXCHANGE)
                return TryDecodePair(immediateByte, out _, out _);
            return TryDecodeSingle(immediateByte, out _);
        }
    }
}
