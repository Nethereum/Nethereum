using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    public static class TransactionDecoders
    {
        public static ITransactionDecoder ForFork(HardforkName fork)
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
                    return LegacyOnlyTransactionDecoder.Instance;
                case HardforkName.Berlin:
                    return Eip2930TransactionDecoder.Instance;
                case HardforkName.London:
                case HardforkName.ArrowGlacier:
                case HardforkName.GrayGlacier:
                case HardforkName.Paris:
                case HardforkName.Shanghai:
                    return Eip1559TransactionDecoder.Instance;
                case HardforkName.Cancun:
                    return Eip4844TransactionDecoder.Instance;
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                case HardforkName.OsakaBpo2:
                case HardforkName.Amsterdam:
                    return Eip7702TransactionDecoder.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No transaction decoder registered for fork {fork}. " +
                        "Add a fork-spec entry (TransactionDecoder) and extend TransactionDecoders.ForFork.");
            }
        }
    }
}
