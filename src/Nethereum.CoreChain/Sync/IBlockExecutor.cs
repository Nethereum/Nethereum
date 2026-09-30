using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface IBlockExecutor
    {
        Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals,
            CancellationToken ct);
    }
}
