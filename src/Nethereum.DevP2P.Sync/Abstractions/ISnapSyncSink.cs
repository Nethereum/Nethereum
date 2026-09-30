using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface ISnapSyncSink
    {
        ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct);

        ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct);

        ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct);

        ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct);

        ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct);
    }

    public interface IStorageScope
    {
        ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct);

        ValueTask EndAsync(CancellationToken ct);

        ValueTask AbortAsync(CancellationToken ct);
    }
}
