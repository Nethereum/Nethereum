using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    public static class BlockHeaderCodecs
    {
        public static IBlockHeaderCodec ForFork(HardforkName fork)
        {
            switch (fork)
            {
                case HardforkName.Frontier:
                case HardforkName.FrontierThawing:
                case HardforkName.Homestead:
                case HardforkName.DaoFork:
                case HardforkName.TangerineWhistle:
                case HardforkName.SpuriousDragon:
                case HardforkName.Byzantium:
                case HardforkName.Constantinople:
                case HardforkName.Petersburg:
                case HardforkName.Istanbul:
                case HardforkName.MuirGlacier:
                case HardforkName.Berlin:
                    return LegacyBlockHeaderCodec.Instance;
                case HardforkName.London:
                case HardforkName.ArrowGlacier:
                case HardforkName.GrayGlacier:
                case HardforkName.Paris:
                    return LondonBlockHeaderCodec.Instance;
                case HardforkName.Shanghai:
                    return ShanghaiBlockHeaderCodec.Instance;
                case HardforkName.Cancun:
                    return CancunBlockHeaderCodec.Instance;
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                case HardforkName.OsakaBpo2:
                    return PragueBlockHeaderCodec.Instance;
                case HardforkName.Amsterdam:
                    return AmsterdamBlockHeaderCodec.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No block-header codec registered for fork {fork}. " +
                        "Add a fork-spec entry (HeaderCodec) and extend BlockHeaderCodecs.ForFork.");
            }
        }
    }
}
