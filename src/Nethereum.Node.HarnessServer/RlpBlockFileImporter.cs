using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;

namespace Nethereum.Node.HarnessServer
{
    public sealed class RlpBlockImportSummary
    {
        public static readonly RlpBlockImportSummary Empty = new RlpBlockImportSummary();
        public int Imported { get; init; }
        public int Rejected { get; init; }
        public IReadOnlyList<string> Rejections { get; init; } = Array.Empty<string>();
        public bool AnyBlocksPresent => Imported + Rejected > 0;
    }

    public sealed class RlpBlockFileImporter
    {
        private readonly BlockImporter _importer;

        public RlpBlockFileImporter(BlockImporter importer)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public async Task<RlpBlockImportSummary> ImportAsync(
            string chainRlpPath, string blocksDirectory, CancellationToken ct = default)
        {
            var imported = 0;
            var rejections = new List<string>();

            foreach (var block in ReadBlocksInImportOrder(chainRlpPath, blocksDirectory))
            {
                var rejection = await TryImportBlockAsync(block, ct).ConfigureAwait(false);
                if (rejection == null) imported++;
                else rejections.Add(rejection);
            }

            return new RlpBlockImportSummary { Imported = imported, Rejected = rejections.Count, Rejections = rejections };
        }

        private static IEnumerable<byte[]> ReadBlocksInImportOrder(
            string chainRlpPath, string blocksDirectory)
        {
            if (File.Exists(chainRlpPath))
                foreach (var block in SplitConsecutiveBlocks(File.ReadAllBytes(chainRlpPath)))
                    yield return block;

            if (Directory.Exists(blocksDirectory))
                foreach (var file in EnumerateBlockFilesInNumericOrder(blocksDirectory))
                    yield return File.ReadAllBytes(file);
        }

        private static IEnumerable<byte[]> SplitConsecutiveBlocks(byte[] bytes)
        {
            var reader = new RlpReader(bytes);
            while (!reader.IsAtEnd())
            {
                if (reader.PeekKind() == RlpKind.List) yield return reader.ReadRaw();
                else reader.Skip();
            }
        }

        private static IEnumerable<string> EnumerateBlockFilesInNumericOrder(string directory) =>
            Directory.EnumerateFiles(directory, "*.rlp")
                .OrderBy(path => LeadingNumberOrLast(Path.GetFileName(path)));

        private static long LeadingNumberOrLast(string fileName)
        {
            var digits = new string(fileName.TakeWhile(char.IsDigit).ToArray());
            return digits.Length == 0 ? long.MaxValue : long.Parse(digits);
        }

        private async Task<string> TryImportBlockAsync(byte[] blockBytes, CancellationToken ct)
        {
            BlockHeader header = null;
            try
            {
                var block = NewBlockMessageEncoder.DecodeBlock(blockBytes);
                header = block.Header;
                if (block.Withdrawals == null && header.WithdrawalsRoot != null)
                    throw new InvalidOperationException("The block header declares a withdrawalsRoot but the block carries no withdrawals list.");
                var result = await _importer.ImportAsync(
                    header,
                    block.Transactions,
                    block.Uncles.Count == 0 ? null : block.Uncles,
                    block.Withdrawals,
                    ct).ConfigureAwait(false);
                return result.BlockHash != null
                    ? null
                    : $"block {header.BlockNumber}: {result.DescribeRejection()}";
            }
            catch (Exception ex)
            {
                return $"block {(header != null ? header.BlockNumber.ToString() : "?")}: {ex.GetType().Name}: {ex.Message}";
            }
        }
    }
}
