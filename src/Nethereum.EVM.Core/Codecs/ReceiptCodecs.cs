using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    public static class ReceiptCodecs
    {
        public static IReceiptCodec ForFork(HardforkName fork)
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
                    return LegacyReceiptCodec.Instance;
                case HardforkName.Berlin:
                case HardforkName.London:
                case HardforkName.ArrowGlacier:
                case HardforkName.GrayGlacier:
                case HardforkName.Paris:
                case HardforkName.Shanghai:
                case HardforkName.Cancun:
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                case HardforkName.OsakaBpo2:
                case HardforkName.Amsterdam:
                    return Eip2718ReceiptCodec.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No receipt codec registered for fork {fork}. " +
                        "Add a fork-spec entry (ReceiptCodec) and extend ReceiptCodecs.ForFork.");
            }
        }
    }
}
