using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        private readonly AsyncLocal<CancellationToken> _rpcEvmDeadline = new AsyncLocal<CancellationToken>();

        internal async Task<T> WithinRpcEvmDeadlineAsync<T>(Func<Task<T>> execute)
        {
            var timeout = Config.RpcEvmTimeout;
            if (timeout <= TimeSpan.Zero || _rpcEvmDeadline.Value.CanBeCanceled) return await execute();

            using var deadline = new CancellationTokenSource(timeout);
            _rpcEvmDeadline.Value = deadline.Token;
            try
            {
                return await execute();
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                throw ExecutionAborted(timeout);
            }
            finally
            {
                _rpcEvmDeadline.Value = default;
            }
        }

        private static RpcException ExecutionAborted(TimeSpan timeout) =>
            new RpcException(-32000, $"execution aborted (timeout = {timeout.TotalSeconds}s)");

        private static async Task<T> RunWithinTimeoutAsync<T>(
            TimeSpan timeout, ExecutionStateService executionState, Func<Task<T>> execute)
        {
            if (timeout <= TimeSpan.Zero) return await execute();

            using var deadline = new CancellationTokenSource(timeout);
            executionState.CancellationToken = deadline.Token;
            try
            {
                return await execute();
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                throw ExecutionAborted(timeout);
            }
            finally
            {
                executionState.CancellationToken = default;
            }
        }
    }
}
