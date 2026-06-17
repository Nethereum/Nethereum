using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Maps a beacon-chain slot to the active <see cref="ConsensusFork"/>.
    /// Mainnet defaults via <see cref="Mainnet"/>; custom chains construct with their own activation table.
    /// </summary>
    public sealed class ChainSpec
    {
        private readonly IReadOnlyList<(ulong Slot, ConsensusFork Fork)> _activations;

        public ulong SlotsPerEpoch { get; }
        public ulong SecondsPerSlot { get; }

        public ChainSpec(IEnumerable<(ulong Slot, ConsensusFork Fork)> activations,
                        ulong slotsPerEpoch = 32, ulong secondsPerSlot = 12)
        {
            if (activations == null) throw new ArgumentNullException(nameof(activations));
            _activations = activations.OrderBy(a => a.Slot).ToList();
            if (_activations.Count == 0)
                throw new ArgumentException("At least one fork activation required.", nameof(activations));
            SlotsPerEpoch = slotsPerEpoch;
            SecondsPerSlot = secondsPerSlot;
        }

        public ConsensusFork GetForkAtSlot(ulong slot)
        {
            var fork = _activations[0].Fork;
            for (int i = 0; i < _activations.Count; i++)
            {
                if (slot >= _activations[i].Slot) fork = _activations[i].Fork;
                else break;
            }
            return fork;
        }

        public static readonly ChainSpec Mainnet = new ChainSpec(new (ulong, ConsensusFork)[]
        {
            (         0, ConsensusFork.Altair),
            ( 4_636_672, ConsensusFork.Bellatrix),
            ( 6_209_536, ConsensusFork.Capella),
            ( 8_626_176, ConsensusFork.Deneb),
            (11_649_024, ConsensusFork.Electra),
        });
    }
}
