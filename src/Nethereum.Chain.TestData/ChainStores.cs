using Nethereum.CoreChain.Storage;

namespace Nethereum.Chain.TestData
{
    public sealed record ChainStores(
        IBlockStore Blocks,
        IStateStore State,
        ITransactionStore Transactions,
        IReceiptStore Receipts,
        ILogStore Logs,
        IBlockAccessListStore BlockAccessLists)
    {
        public static ChainStores From(IChainStoreBundle bundle) =>
            new ChainStores(bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                bundle.BlockAccessLists);
    }
}
