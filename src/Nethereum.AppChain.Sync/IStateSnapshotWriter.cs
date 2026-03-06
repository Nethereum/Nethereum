using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IStateSnapshotWriter
    {
        Task<StateSnapshotInfo> WriteSnapshotAsync(
            BigInteger blockNumber,
            Stream outputStream,
            CancellationToken cancellationToken = default);

        Task<StateSnapshotInfo> WriteSnapshotToFileAsync(
            BigInteger blockNumber,
            string filePath,
            bool compress = true,
            CancellationToken cancellationToken = default);
    }
}
