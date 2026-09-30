using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class MempoolTransactionSubmissionService : ITransactionSubmissionService
    {
        private readonly RelayMempool _mempool;

        public MempoolTransactionSubmissionService(RelayMempool mempool)
        {
            _mempool = mempool ?? throw new ArgumentNullException(nameof(mempool));
        }

        public async Task<TransactionExecutionResult> SubmitAsync(ISignedTransaction transaction, CancellationToken cancellationToken = default)
        {
            var result = await _mempool.SubmitAsync(transaction, cancellationToken).ConfigureAwait(false);
            if (result.Accepted)
            {
                return new TransactionExecutionResult
                {
                    Success = true,
                    Transaction = transaction,
                    TransactionHash = result.TransactionHash,
                };
            }

            return new TransactionExecutionResult
            {
                Success = false,
                RevertReason = DescribeRejection(result),
            };
        }

        private static string DescribeRejection(MempoolSubmitResult result)
            => string.IsNullOrEmpty(result.RejectMessage)
                ? result.RejectReason.ToString()
                : $"{result.RejectReason}: {result.RejectMessage}";
    }
}
