using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IStateSnapshotImporter
    {
        Task<StateSnapshotImportResult> ImportSnapshotAsync(
            Stream snapshotStream,
            byte[]? expectedStateRoot = null,
            bool verifyStateRoot = true,
            CancellationToken cancellationToken = default);

        Task<StateSnapshotImportResult> ImportSnapshotFromFileAsync(
            string filePath,
            byte[]? expectedStateRoot = null,
            bool verifyStateRoot = true,
            bool compressed = true,
            CancellationToken cancellationToken = default);
    }

    public class StateSnapshotImportResult
    {
        public bool Success { get; set; }
        public StateSnapshotInfo? SnapshotInfo { get; set; }
        public long AccountsImported { get; set; }
        public long StorageSlotsImported { get; set; }
        public long CodesImported { get; set; }
        public string? ErrorMessage { get; set; }
        public bool StateRootVerified { get; set; }
        public byte[]? ComputedStateRoot { get; set; }
    }
}
