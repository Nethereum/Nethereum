using System;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    /// <summary>
    /// Configuration toggles and checkpoints required to initialise a beacon light client.
    /// Defaults populate <see cref="GenesisValidatorsRoot"/> and <see cref="WeakSubjectivityRoot"/>
    /// as 32-byte zero arrays so length asserts pass at construction; operators must override both
    /// before <see cref="LightClientService.InitializeAsync"/> for a valid signing domain or
    /// trust-anchor lookup per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/light-client.md">
    /// specs/altair/light-client/light-client.md</see> (<c>initialize_light_client_store</c>).
    /// </summary>
    public class LightClientConfig
    {
        private byte[] _genesisValidatorsRoot = new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength];
        private byte[] _weakSubjectivityRoot = new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength];

        /// <summary>
        /// <c>genesis_validators_root</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 938 (<c>compute_fork_data_root</c>): exactly
        /// 32 bytes. Setter rejects any other length.
        /// </summary>
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

        /// <summary>
        /// Trusted block root used to fetch the <c>LightClientBootstrap</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/light-client.md">
        /// specs/altair/light-client/light-client.md</see> (<c>initialize_light_client_store</c>):
        /// exactly 32 bytes. Setter rejects any other length.
        /// </summary>
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

        /// <summary>
        /// Fork activation schedule used by <see cref="ChainSpec.GetForkAtSlot"/> when stamping
        /// inbound SSZ containers with their active <see cref="ConsensusFork"/> and by
        /// <see cref="ChainSpec.GetForkVersionAtSlot"/> when deriving the signing domain per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 451–454. Defaults to mainnet.
        /// </summary>
        public ChainSpec ChainSpec { get; set; } = ChainSpec.Mainnet;
    }
}
