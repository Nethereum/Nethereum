using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IBodyFillCursor
    {
        ulong Get();

        Task AdvanceAsync(ulong highestPersisted, CancellationToken ct);

        void Requeue(ulong fromBlock) { }
    }

    public sealed class ExecutionHeadBodyFillCursor : IBodyFillCursor
    {
        private readonly IChainStoreBundle _bundle;
        private readonly object _gate = new object();
        private ulong _fetchedHighWater;

        public ExecutionHeadBodyFillCursor(IChainStoreBundle bundle)
        {
            _bundle = bundle ?? throw new System.ArgumentNullException(nameof(bundle));
        }

        public ulong Get()
        {
            var executionHead = _bundle.Metadata.GetLastBlock();
            lock (_gate) return executionHead > _fetchedHighWater ? executionHead : _fetchedHighWater;
        }

        public Task AdvanceAsync(ulong highestPersisted, CancellationToken ct)
        {
            lock (_gate)
                if (highestPersisted > _fetchedHighWater) _fetchedHighWater = highestPersisted;
            return Task.CompletedTask;
        }

        public void Requeue(ulong fromBlock)
        {
            var ceiling = fromBlock == 0 ? 0UL : fromBlock - 1;
            lock (_gate)
                if (_fetchedHighWater > ceiling) _fetchedHighWater = ceiling;
        }
    }
}
