using System;

namespace Nethereum.Util.HashProviders
{
    public class PoseidonHashProvider : IHashProvider
    {
        private readonly PoseidonEvmHasher _hasher;

        public PoseidonHashProvider()
            : this(PoseidonParameterPreset.CircomT3)
        {
        }

        public PoseidonHashProvider(PoseidonParameterPreset preset)
        {
            _hasher = new PoseidonEvmHasher(preset);
        }

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return _hasher.HashBytesToBytes(data);
        }
    }
}
