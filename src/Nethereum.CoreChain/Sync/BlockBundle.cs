using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public sealed record BlockBundle(
        BlockHeader Header,
        IList<ISignedTransaction> Transactions,
        IList<BlockHeader> Uncles,
        IList<Withdrawal> Withdrawals,
        byte[] HeaderHash);
}
