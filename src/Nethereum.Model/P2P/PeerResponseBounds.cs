using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-peer-limits", "PeerResponseBounds")]
    public static class PeerResponseBounds
    {
        /// <summary>
        /// Yellow paper G_transaction: the intrinsic cost every transaction pays
        /// before it does anything, so no block fits more than gasLimit/21000 of them.
        /// </summary>
        public const int MaxTransactionAnnouncements = 5000;

        public const int MinimumTransactionGas = 21000;

        public const long ImplausibleBlockGasLimit = 1000000000;

        public const int MaxTransactionsPerBlock =
            (int)(ImplausibleBlockGasLimit / MinimumTransactionGas);

        public static bool ExceedsWhatABlockCanHold(RLPCollection perBlockItems)
        {
            return perBlockItems != null && perBlockItems.Count > MaxTransactionsPerBlock;
        }
    }
}
