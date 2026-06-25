using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Maps a beacon-chain slot to the active <see cref="ConsensusFork"/> and its 4-byte fork
    /// version. Backs <c>compute_fork_version(compute_epoch_at_slot(slot))</c> per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/fulu/p2p-interface.md">
    /// specs/fulu/p2p-interface.md</see> lines 110–128. Mainnet activations live in
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
    /// configs/mainnet.yaml</see>; custom chains construct with their own activation table.
    /// </summary>
    public sealed class ChainSpec
    {
        /// <summary>
        /// Single row of the slot &#8594; fork activation schedule:
        /// <c>StartSlot = activation_epoch * SLOTS_PER_EPOCH</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 908 (<c>compute_epoch_at_slot</c>), with the
        /// 4-byte <c>ForkVersion</c> from <c>configs/mainnet.yaml</c>.
        /// </summary>
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

        /// <summary>
        /// <c>SLOTS_PER_EPOCH = uint64(2**5) = 32</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 265.
        /// </summary>
        public ulong SlotsPerEpoch { get; }

        /// <summary>
        /// <c>SECONDS_PER_SLOT = 12</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see>.
        /// </summary>
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

        /// <summary>
        /// Returns the <see cref="ConsensusFork"/> active at <paramref name="slot"/> — the latest
        /// row whose <see cref="ForkActivation.StartSlot"/> is &lt;= <paramref name="slot"/>.
        /// Throws <see cref="NotSupportedException"/> if the resolved fork is
        /// <see cref="ConsensusFork.Gloas"/>: <c>GLOAS_FORK_EPOCH = FAR_FUTURE_EPOCH</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> line 60, i.e. not yet scheduled on mainnet.
        /// </summary>
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

        /// <summary>
        /// Returns the 4-byte fork version active at <paramref name="slot"/> per
        /// <c>compute_fork_version</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/fulu/p2p-interface.md">
        /// specs/fulu/p2p-interface.md</see> lines 110–128. Consumed by signing-domain derivation
        /// (BLS verify) per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 451–454. Defensively copies the
        /// stored row so callers cannot mutate the activation table. Throws
        /// <see cref="NotSupportedException"/> if the resolved fork is Gloas.
        /// </summary>
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

        /// <summary>
        /// Mainnet activation schedule per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see>. Each <c>StartSlot</c> is <c>activation_epoch *
        /// SLOTS_PER_EPOCH</c> (SLOTS_PER_EPOCH = 32 per specs/phase0/beacon-chain.md line 265):
        /// <list type="bullet">
        /// <item>Phase0: epoch 0 (genesis) &#8594; slot 0, version 0x00000000 (mainnet.yaml line 30).</item>
        /// <item>Altair: epoch 74240 &#8594; slot 2,375,680, version 0x01000000 (mainnet.yaml lines 41–42).</item>
        /// <item>Bellatrix: epoch 144896 &#8594; slot 4,636,672, version 0x02000000 (mainnet.yaml lines 44–45).</item>
        /// <item>Capella: epoch 194048 &#8594; slot 6,209,536, version 0x03000000 (mainnet.yaml lines 47–48).</item>
        /// <item>Deneb: epoch 269568 &#8594; slot 8,626,176, version 0x04000000 (mainnet.yaml lines 50–51).</item>
        /// <item>Electra: epoch 364032 &#8594; slot 11,649,024, version 0x05000000 (mainnet.yaml lines 53–54).</item>
        /// <item>Fulu: epoch 411392 &#8594; slot 13,164,544, version 0x06000000 (mainnet.yaml lines 56–57).</item>
        /// <item>Gloas: <c>FAR_FUTURE_EPOCH</c> sentinel at <see cref="ulong.MaxValue"/> &#8594; throws on lookup
        /// (mainnet.yaml lines 59–60).</item>
        /// </list>
        /// </summary>
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
