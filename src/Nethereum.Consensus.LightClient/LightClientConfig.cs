using System;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    public class LightClientConfig
    {
        private byte[] _genesisValidatorsRoot = new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength];
        private byte[] _weakSubjectivityRoot = new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength];

        public byte[] GenesisValidatorsRoot
        {
            get => _genesisValidatorsRoot;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length != Nethereum.Consensus.Ssz.SszBasicTypes.RootLength)
                    throw new InvalidOperationException(
                        $"GenesisValidatorsRoot must be exactly {Nethereum.Consensus.Ssz.SszBasicTypes.RootLength} bytes; got {value.Length}.");
                _genesisValidatorsRoot = value;
            }
        }

        public ulong SecondsPerSlot { get; set; } = 12;

        public byte[] WeakSubjectivityRoot
        {
            get => _weakSubjectivityRoot;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length != Nethereum.Consensus.Ssz.SszBasicTypes.RootLength)
                    throw new InvalidOperationException(
                        $"WeakSubjectivityRoot must be exactly {Nethereum.Consensus.Ssz.SszBasicTypes.RootLength} bytes; got {value.Length}.");
                _weakSubjectivityRoot = value;
            }
        }

        public ulong WeakSubjectivityPeriod { get; set; } = 256 * 32;

        public ChainSpec ChainSpec { get; set; } = ChainSpec.Mainnet;
    }
}
