using System;

namespace Nethereum.Merkle.Binary.Keys
{
    public static class CodeChunker
    {
        private const byte Push1 = 0x60;
        private const byte Push32 = 0x7f;
        private const byte PushOffset = 0x5f;

        public static byte[][] ChunkifyCode(byte[] code)
        {
            if (code == null || code.Length == 0)
                return Array.Empty<byte[]>();

            int padded = code.Length;
            int rem = code.Length % BinaryTrieConstants.StemSize;
            if (rem != 0)
                padded += BinaryTrieConstants.StemSize - rem;

            var paddedCode = new byte[padded];
            Array.Copy(code, 0, paddedCode, 0, code.Length);

            var execData = new int[padded + 32];
            int pos = 0;
            while (pos < padded)
            {
                byte b = paddedCode[pos];
                int pushdataBytes = 0;
                if (b >= Push1 && b <= Push32)
                    pushdataBytes = b - PushOffset;
                pos++;
                for (int x = 0; x < pushdataBytes; x++)
                {
                    if (pos + x < execData.Length)
                        execData[pos + x] = pushdataBytes - x;
                }
                pos += pushdataBytes;
            }

            int numChunks = padded / BinaryTrieConstants.StemSize;
            var chunks = new byte[numChunks][];

            for (int i = 0; i < numChunks; i++)
            {
                int offset = i * BinaryTrieConstants.StemSize;
                var chunk = new byte[BinaryTrieConstants.HashSize];
                int leading = execData[offset];
                if (leading > BinaryTrieConstants.StemSize)
                    leading = BinaryTrieConstants.StemSize;
                chunk[0] = (byte)leading;
                Array.Copy(paddedCode, offset, chunk, 1, BinaryTrieConstants.StemSize);
                chunks[i] = chunk;
            }

            return chunks;
        }
    }
}
