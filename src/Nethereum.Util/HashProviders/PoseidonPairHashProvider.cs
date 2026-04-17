using System;

namespace Nethereum.Util.HashProviders
{
    public class PoseidonPairHashProvider : IHashProvider
    {
        private readonly PoseidonEvmHasher _hasher;

        public PoseidonPairHashProvider()
        {
            _hasher = new PoseidonEvmHasher(PoseidonParameterPreset.CircomT2);
        }

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length == 32)
                return _hasher.HashBytesToBytes(data);

            if (data.Length == 64)
            {
                var left = new byte[32];
                var right = new byte[32];
                Array.Copy(data, 0, left, 0, 32);
                Array.Copy(data, 32, right, 0, 32);
                return _hasher.HashBytesToBytes(left, right);
            }

            throw new ArgumentException(
                $"PoseidonPairHashProvider expects 32 or 64 bytes (CircomT2 with 1 or 2 inputs), got {data.Length}",
                nameof(data));
        }
    }
}
