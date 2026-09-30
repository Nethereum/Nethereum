using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface ITransactionSubmissionService
    {
        Task<TransactionExecutionResult> SubmitAsync(ISignedTransaction transaction, CancellationToken cancellationToken = default);
    }
}
