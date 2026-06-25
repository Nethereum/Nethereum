using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Static fork → transaction decoder lookup. Each EIP-2718 typed tx
    /// activates at a specific fork:
    /// <list type="bullet">
    ///   <item>Berlin (EIP-2930) — adds type 0x01 access-list tx.</item>
    ///   <item>London (EIP-1559) — adds type 0x02 dynamic-fee tx.</item>
    ///   <item>Cancun (EIP-4844) — adds type 0x03 blob tx.</item>
    ///   <item>Prague (EIP-7702) — adds type 0x04 set-code tx.</item>
    /// </list>
    /// Each fork's decoder rejects tx-type bytes not yet active so peers
    /// can't poison pre-fork blocks with future tx types.
    /// </summary>
    public static class TransactionDecoders
    {
        public static ITransactionDecoder ForFork(HardforkName fork)
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
                    return LegacyOnlyTransactionDecoder.Instance;
                case HardforkName.Berlin:
                    return Eip2930TransactionDecoder.Instance;
                case HardforkName.London:
                case HardforkName.Paris:
                case HardforkName.Shanghai:
                    return Eip1559TransactionDecoder.Instance;
                case HardforkName.Cancun:
                    return Eip4844TransactionDecoder.Instance;
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                    return Eip7702TransactionDecoder.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No transaction decoder registered for fork {fork}. " +
                        "Add a fork-spec entry (TransactionDecoder) and extend TransactionDecoders.ForFork.");
            }
        }
    }
}
