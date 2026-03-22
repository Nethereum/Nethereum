using System;
using System.Numerics;

namespace Nethereum.Util.HashProviders
{
    public class PoseidonPairHashProvider : IHashProvider
    {
        private readonly PoseidonHasher _hasher;

        public PoseidonPairHashProvider()
            : this(new PoseidonHasher(PoseidonParameterPreset.CircomT2))
        {
        }

        public PoseidonPairHashProvider(PoseidonHasher hasher)
        {
            _hasher = hasher ?? new PoseidonHasher(PoseidonParameterPreset.CircomT2);
        }

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length == 32)
            {
                return _hasher.HashBytesToBytes(data);
            }

            if (data.Length == 64)
            {
                var left = new byte[32];
                var right = new byte[32];
                Array.Copy(data, 0, left, 0, 32);
                Array.Copy(data, 32, right, 0, 32);
                return _hasher.HashBytesToBytes(left, right);
            }

            return _hasher.HashBytesToBytes(data);
        }
    }
}
