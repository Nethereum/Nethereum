using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IStateSnapshotReader
    {
        Task<StateSnapshotHeader> ReadHeaderAsync(Stream inputStream, CancellationToken cancellationToken = default);

        IAsyncEnumerable<StateAccount> ReadAccountsAsync(Stream inputStream, CancellationToken cancellationToken = default);

        IAsyncEnumerable<StateStorageSlot> ReadStorageSlotsAsync(Stream inputStream, string address, CancellationToken cancellationToken = default);

        IAsyncEnumerable<StateCode> ReadCodesAsync(Stream inputStream, CancellationToken cancellationToken = default);

        Task<StateSnapshotInfo> ReadAndVerifyAsync(
            Stream inputStream,
            byte[] expectedStateRoot,
            CancellationToken cancellationToken = default);
    }

    public class StateSnapshotHeader
    {
        public ushort Version { get; set; }
        public ulong ChainId { get; set; }
        public ulong BlockNumber { get; set; }
        public byte[] StateRoot { get; set; } = System.Array.Empty<byte>();
        public ulong AccountCount { get; set; }
        public ulong CodeCount { get; set; }
    }
}
