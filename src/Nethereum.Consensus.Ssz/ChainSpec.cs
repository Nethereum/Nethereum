using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Consensus.Ssz
{
    public sealed class ChainSpec
    {
        public readonly struct ForkActivation
        {
            public ulong StartSlot { get; }
            public ConsensusFork Fork { get; }
            public byte[] ForkVersion { get; }

            public ForkActivation(ulong startSlot, ConsensusFork fork, byte[] forkVersion)
            {
                if (forkVersion == null) throw new ArgumentNullException(nameof(forkVersion));
                if (forkVersion.Length != 4)
                    throw new ArgumentException(
                        "ForkVersion must be exactly 4 bytes (DomainType width per specs/phase0/beacon-chain.md line 209).",
                        nameof(forkVersion));
                StartSlot = startSlot;
                Fork = fork;
                ForkVersion = forkVersion;
            }
        }

        private readonly IReadOnlyList<ForkActivation> _activations;

        public ulong SlotsPerEpoch { get; }

        public ulong SecondsPerSlot { get; }

        public ChainSpec(IEnumerable<ForkActivation> activations,
                        ulong slotsPerEpoch = 32, ulong secondsPerSlot = 12)
        {
            if (activations == null) throw new ArgumentNullException(nameof(activations));
            if (slotsPerEpoch == 0)
                throw new ArgumentException("SlotsPerEpoch must be > 0.", nameof(slotsPerEpoch));
            _activations = activations.OrderBy(a => a.StartSlot).ToList();
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
                if (slot >= _activations[i].StartSlot) fork = _activations[i].Fork;
                else break;
            }
            if (fork == ConsensusFork.Gloas)
                throw new NotSupportedException(
                    "Gloas activation has not been scheduled (GLOAS_FORK_EPOCH = FAR_FUTURE_EPOCH per configs/mainnet.yaml line 60).");
            return fork;
        }

        public byte[] GetForkVersionAtSlot(ulong slot)
        {
            var activation = _activations[0];
            for (int i = 0; i < _activations.Count; i++)
            {
                if (slot >= _activations[i].StartSlot) activation = _activations[i];
                else break;
            }
            if (activation.Fork == ConsensusFork.Gloas)
                throw new NotSupportedException(
                    "Gloas activation has not been scheduled (GLOAS_FORK_EPOCH = FAR_FUTURE_EPOCH per configs/mainnet.yaml line 60).");
            var copy = new byte[4];
            Buffer.BlockCopy(activation.ForkVersion, 0, copy, 0, 4);
            return copy;
        }

        public static readonly ChainSpec Mainnet = new ChainSpec(new[]
        {
            new ForkActivation(             0UL, ConsensusFork.Phase0,    new byte[] { 0x00, 0x00, 0x00, 0x00 }),
            new ForkActivation(     2_375_680UL, ConsensusFork.Altair,    new byte[] { 0x01, 0x00, 0x00, 0x00 }),
            new ForkActivation(     4_636_672UL, ConsensusFork.Bellatrix, new byte[] { 0x02, 0x00, 0x00, 0x00 }),
            new ForkActivation(     6_209_536UL, ConsensusFork.Capella,   new byte[] { 0x03, 0x00, 0x00, 0x00 }),
            new ForkActivation(     8_626_176UL, ConsensusFork.Deneb,     new byte[] { 0x04, 0x00, 0x00, 0x00 }),
            new ForkActivation(    11_649_024UL, ConsensusFork.Electra,   new byte[] { 0x05, 0x00, 0x00, 0x00 }),
            new ForkActivation(    13_164_544UL, ConsensusFork.Fulu,      new byte[] { 0x06, 0x00, 0x00, 0x00 }),
            new ForkActivation(ulong.MaxValue,   ConsensusFork.Gloas,     new byte[] { 0x07, 0x00, 0x00, 0x00 }),
        });
    }
}
