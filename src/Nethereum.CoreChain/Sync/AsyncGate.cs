using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Sync
{
    public sealed class AsyncGate
    {
        private sealed class ExecutedHead
        {
            public ExecutedHead(ulong number, byte[] hash)
            {
                Number = number;
                Hash = hash;
            }

            public ulong Number { get; }
            public byte[] Hash { get; }
        }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private volatile IBlockExecutor _executor;
        private volatile ExecutedHead _head;

        public IBlockExecutor Executor => _executor;

        public bool HasHead => _head != null;

        public ulong HeadNumber => _head?.Number ?? 0UL;

        public byte[] HeadHash => _head?.Hash;

        public void Publish(IBlockExecutor executor) => _executor = executor;

        public void SetHead(ulong number, byte[] hash) => _head = new ExecutedHead(number, hash);

        public Task WaitAsync(CancellationToken ct = default) => _semaphore.WaitAsync(ct);

        public void Release() => _semaphore.Release();
    }
}
