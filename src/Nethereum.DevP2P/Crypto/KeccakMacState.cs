using System;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.DevP2P.Crypto
{
    public class KeccakMacState
    {
        private KeccakDigest _digest;

        private KeccakMacState(KeccakDigest digest)
        {
            _digest = digest;
        }

        public static KeccakMacState Init(byte[] xorKey, byte[] packet)
        {
            var digest = new KeccakDigest(256);
            digest.BlockUpdate(xorKey, 0, xorKey.Length);
            digest.BlockUpdate(packet, 0, packet.Length);
            return new KeccakMacState(digest);
        }

        public void Update(byte[] data)
        {
            _digest.BlockUpdate(data, 0, data.Length);
        }

        public void Update(byte[] data, int offset, int length)
        {
            _digest.BlockUpdate(data, offset, length);
        }

        public byte[] DigestFirst16()
        {
            var copy = new KeccakDigest(_digest);
            var full = new byte[32];
            copy.DoFinal(full, 0);
            var result = new byte[16];
            Buffer.BlockCopy(full, 0, result, 0, 16);
            return result;
        }

        public KeccakMacState Clone()
        {
            return new KeccakMacState(new KeccakDigest(_digest));
        }
    }
}
