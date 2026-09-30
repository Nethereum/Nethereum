using System;

namespace Nethereum.CoreChain.Proving
{
    public static class BlockProofSubmitter
    {
        public static byte[] SerializeProofPayload(BlockProofResult proof)
        {
            var preRoot = proof.PreStateRoot ?? new byte[32];
            var postRoot = proof.PostStateRoot ?? new byte[32];
            var proofBytes = proof.ProofBytes ?? new byte[0];
            var witnessHash = proof.WitnessHash ?? new byte[32];

            var blockNumberBytes = BitConverter.GetBytes(proof.BlockNumber);
            var modeBytes = System.Text.Encoding.UTF8.GetBytes(proof.ProverMode ?? "Unknown");

            var payload = new byte[4 + 32 + 32 + 32 + 8 + 4 + modeBytes.Length + proofBytes.Length];
            int offset = 0;

            WriteInt32(payload, ref offset, 1);
            WriteBytes(payload, ref offset, preRoot, 32);
            WriteBytes(payload, ref offset, postRoot, 32);
            WriteBytes(payload, ref offset, witnessHash, 32);
            Buffer.BlockCopy(blockNumberBytes, 0, payload, offset, 8); offset += 8;
            WriteInt32(payload, ref offset, modeBytes.Length);
            Buffer.BlockCopy(modeBytes, 0, payload, offset, modeBytes.Length); offset += modeBytes.Length;
            Buffer.BlockCopy(proofBytes, 0, payload, offset, proofBytes.Length);

            return payload;
        }

        public static BlockProofResult DeserializeProofPayload(byte[] payload)
        {
            int offset = 0;
            var version = ReadInt32(payload, ref offset);

            var preRoot = new byte[32];
            Buffer.BlockCopy(payload, offset, preRoot, 0, 32); offset += 32;

            var postRoot = new byte[32];
            Buffer.BlockCopy(payload, offset, postRoot, 0, 32); offset += 32;

            var witnessHash = new byte[32];
            Buffer.BlockCopy(payload, offset, witnessHash, 0, 32); offset += 32;

            var blockNumber = BitConverter.ToInt64(payload, offset); offset += 8;

            var modeLen = ReadInt32(payload, ref offset);
            var mode = System.Text.Encoding.UTF8.GetString(payload, offset, modeLen); offset += modeLen;

            var proofBytes = new byte[payload.Length - offset];
            if (proofBytes.Length > 0)
                Buffer.BlockCopy(payload, offset, proofBytes, 0, proofBytes.Length);

            return new BlockProofResult
            {
                PreStateRoot = preRoot,
                PostStateRoot = postRoot,
                WitnessHash = witnessHash,
                BlockNumber = blockNumber,
                ProverMode = mode,
                ProofBytes = proofBytes
            };
        }

        private static void WriteInt32(byte[] buf, ref int offset, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buf, offset, 4);
            offset += 4;
        }

        private static int ReadInt32(byte[] buf, ref int offset)
        {
            var val = BitConverter.ToInt32(buf, offset);
            offset += 4;
            return val;
        }

        private static void WriteBytes(byte[] buf, ref int offset, byte[] src, int len)
        {
            var toCopy = Math.Min(src.Length, len);
            Buffer.BlockCopy(src, 0, buf, offset, toCopy);
            offset += len;
        }
    }
}
