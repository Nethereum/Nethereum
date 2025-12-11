using System;

namespace Nethereum.Consensus.LightClient
{
    /// <summary>
    /// Represents the configuration toggles and checkpoints required to initialise a beacon light client.
    /// </summary>
    public class LightClientConfig
    {
        public byte[] GenesisValidatorsRoot { get; set; } = Array.Empty<byte>();
        public byte[] CurrentForkVersion { get; set; } = new byte[4];
        public ulong SlotsPerEpoch { get; set; } = 32;
        public ulong SecondsPerSlot { get; set; } = 12;
        public byte[] WeakSubjectivityRoot { get; set; } = Array.Empty<byte>();
        public ulong WeakSubjectivityPeriod { get; set; } = 256 * 32;
    }
}
