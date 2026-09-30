using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface ITransactionOrderingPolicy
    {
        IReadOnlyList<TxEntry> Order(
            IEnumerable<ISignedTransaction> pool,
            BlockContext blockContext,
            BigInteger gasLimit,
            CancellationToken ct);
    }
}
