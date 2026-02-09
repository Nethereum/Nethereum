using System;

namespace Nethereum.Util.HashProviders
{
    public class PoseidonHashProvider : IHashProvider
    {
        private readonly PoseidonHasher _hasher;

        public PoseidonHashProvider()
            : this(null)
        {
        }

        public PoseidonHashProvider(PoseidonParameterPreset preset)
            : this(new PoseidonHasher(preset))
        {
        }

        public PoseidonHashProvider(PoseidonHasher hasher)
        {
            _hasher = hasher ?? new PoseidonHasher();
        }

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return _hasher.HashBytesToBytes(data);
        }
    }
}
