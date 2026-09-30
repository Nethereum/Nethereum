using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IBodyFillCursor
    {
        ulong Get();

        Task AdvanceAsync(ulong highestPersisted, CancellationToken ct);
    }

    public sealed class ExecutionHeadBodyFillCursor : IBodyFillCursor
    {
        private readonly IChainStoreBundle _bundle;
        private ulong _fetchedHighWater;

        public ExecutionHeadBodyFillCursor(IChainStoreBundle bundle)
        {
            _bundle = bundle ?? throw new System.ArgumentNullException(nameof(bundle));
        }

        public ulong Get()
        {
            var executionHead = _bundle.Metadata.GetLastBlock();
            return executionHead > _fetchedHighWater ? executionHead : _fetchedHighWater;
        }

        public Task AdvanceAsync(ulong highestPersisted, CancellationToken ct)
        {
            if (highestPersisted > _fetchedHighWater) _fetchedHighWater = highestPersisted;
            return Task.CompletedTask;
        }
    }
}
