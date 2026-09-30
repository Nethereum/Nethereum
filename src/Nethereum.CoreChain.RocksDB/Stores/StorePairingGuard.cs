using System;
using System.IO;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class StorePairingGuard
    {
        internal const string MarkerFileName = ".pairing-id";

        private readonly string _coreDir;
        private readonly string _historyDir;

        public StorePairingGuard(string coreDir, string historyDir)
        {
            _coreDir = coreDir ?? throw new ArgumentNullException(nameof(coreDir));
            _historyDir = historyDir ?? throw new ArgumentNullException(nameof(historyDir));
        }

        public void EnsurePaired()
        {
            var corePath = Path.Combine(_coreDir, MarkerFileName);
            var historyPath = Path.Combine(_historyDir, MarkerFileName);
            var coreExists = File.Exists(corePath);
            var historyExists = File.Exists(historyPath);

            if (!coreExists && !historyExists)
            {
                var id = Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(_coreDir);
                Directory.CreateDirectory(_historyDir);
                File.WriteAllText(corePath, id);
                File.WriteAllText(historyPath, id);
                return;
            }

            if (coreExists != historyExists)
            {
                var present = coreExists ? _coreDir : _historyDir;
                var missing = coreExists ? _historyDir : _coreDir;
                throw new InvalidOperationException(
                    $"StorePairingGuard: split-store pairing marker found in '{present}' but not in '{missing}'. " +
                    "This is a partial-directory hazard — the two directories that make up a split store do not " +
                    "look like the same pair (e.g. one was copied/restored/deleted independently of the other, or " +
                    "a freshly-created directory was paired with an already-used one). Refusing to boot; make both " +
                    "directories consistent (restore both from the SAME checkpoint, or delete/resync both) before " +
                    "retrying.");
            }

            var coreId = File.ReadAllText(corePath).Trim();
            var historyId = File.ReadAllText(historyPath).Trim();
            if (!string.Equals(coreId, historyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"StorePairingGuard: core store ('{_coreDir}', pairing id '{coreId}') and history store " +
                    $"('{_historyDir}', pairing id '{historyId}') do not share a pairing id — they are not from " +
                    "the same lineage. Refusing to boot; restore both sides from the SAME checkpoint, or resync " +
                    "both from genesis.");
            }
        }

        public static void ClearPairing(string coreDir, string historyDir)
        {
            TryDelete(Path.Combine(coreDir, MarkerFileName));
            TryDelete(Path.Combine(historyDir, MarkerFileName));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
