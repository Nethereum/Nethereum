using System;
using Nethereum.EVM;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Static fork → receipt codec lookup. EIP-2718 typed envelope
    /// activates at Berlin; pre-Berlin receipts are plain RLP.
    ///
    /// <para>Note: this maps to <i>on-wire receipt encoding</i> (RLP vs
    /// EIP-2718 typed envelope), not to receipt construction (post-state
    /// vs status — that's <c>IReceiptConstructionRule</c>, gated by EIP-658
    /// at Byzantium).</para>
    /// </summary>
    public static class ReceiptCodecs
    {
        public static IReceiptCodec ForFork(HardforkName fork)
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
                    return LegacyReceiptCodec.Instance;
                case HardforkName.Berlin:
                case HardforkName.London:
                case HardforkName.Paris:
                case HardforkName.Shanghai:
                case HardforkName.Cancun:
                case HardforkName.Prague:
                case HardforkName.Osaka:
                case HardforkName.OsakaBpo1:
                    return Eip2718ReceiptCodec.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fork),
                        $"No receipt codec registered for fork {fork}. " +
                        "Add a fork-spec entry (ReceiptCodec) and extend ReceiptCodecs.ForFork.");
            }
        }
    }
}
