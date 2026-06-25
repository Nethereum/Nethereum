using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Static fork → header codec lookup. Mirrors the per-fork registration
    /// in <c>Nethereum.EVM.Hardforks.&lt;Fork&gt;Spec.HeaderCodec</c> so
    /// callers that only have a <see cref="HardforkName"/> in hand (peer
    /// message decoders, storage hash recomputation) can grab the right
    /// codec without plumbing a full <c>HardforkConfig</c>.
    ///
    /// <para>Fork → codec mapping reflects on-wire field count, not feature
    /// activation:</para>
    /// <list type="bullet">
    ///   <item>Frontier..Berlin (15 fields) → <see cref="LegacyBlockHeaderCodec"/>.</item>
    ///   <item>London + Paris (16 fields, +baseFee) → <see cref="LondonBlockHeaderCodec"/>.</item>
    ///   <item>Shanghai (17 fields, +withdrawalsRoot) → <see cref="ShanghaiBlockHeaderCodec"/>.</item>
    ///   <item>Cancun (20 fields, +blob fields, +parentBeaconBlockRoot) → <see cref="CancunBlockHeaderCodec"/>.</item>
    ///   <item>Prague onward (21 fields, +requestsHash) → <see cref="PragueBlockHeaderCodec"/>.</item>
    /// </list>
    /// </summary>
    public static class BlockHeaderCodecs
    {
        public static IBlockHeaderCodec ForFork(HardforkName fork)
        {
            switch (fork)
            {
                case HardforkName.Frontier:
                case HardforkName.Homestead:
                case HardforkName.TangerineWhistle:
                case HardforkName.SpuriousDragon:
                case HardforkName.Byzantium:
                case HardforkName.Constantinople:
                case HardforkName.Petersburg:
                case HardforkName.Istanbul:
                case HardforkName.Berlin:
                    return LegacyBlockHeaderCodec.Instance;
                case HardforkName.London:
                case HardforkName.Paris:
                    return LondonBlockHeaderCodec.Instance;
                case HardforkName.Shanghai:
                    return ShanghaiBlockHeaderCodec.Instance;
                case HardforkName.Cancun:
                    return CancunBlockHeaderCodec.Instance;
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                    return PragueBlockHeaderCodec.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No block-header codec registered for fork {fork}. " +
                        "Add a fork-spec entry (HeaderCodec) and extend BlockHeaderCodecs.ForFork.");
            }
        }
    }
}
