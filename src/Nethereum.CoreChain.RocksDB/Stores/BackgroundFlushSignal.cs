using System.Threading.Tasks;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class BackgroundFlushSignal
    {
        private volatile Task _pending = Task.CompletedTask;

        public Task Pending => _pending;

        public bool InFlight => !_pending.IsCompleted;

        public void Track(Task flush) => _pending = flush;
    }
}
