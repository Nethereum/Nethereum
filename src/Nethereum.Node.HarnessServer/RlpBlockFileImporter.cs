using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.Node.HarnessServer
{
    public sealed class RlpBlockImportSummary
    {
        public static readonly RlpBlockImportSummary Empty = new RlpBlockImportSummary();
        public int Imported { get; init; }
        public int Rejected { get; init; }
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
            var rejected = 0;

            foreach (var block in ReadBlocksInImportOrder(chainRlpPath, blocksDirectory))
            {
                if (await TryImportBlockAsync(block, ct).ConfigureAwait(false)) imported++;
                else rejected++;
            }

            return new RlpBlockImportSummary { Imported = imported, Rejected = rejected };
        }

        private static IEnumerable<RLPCollection> ReadBlocksInImportOrder(
            string chainRlpPath, string blocksDirectory)
        {
            if (File.Exists(chainRlpPath))
                foreach (var block in DecodeConsecutiveBlocks(File.ReadAllBytes(chainRlpPath)))
                    yield return block;

            if (Directory.Exists(blocksDirectory))
                foreach (var file in EnumerateBlockFilesInNumericOrder(blocksDirectory))
                    yield return DecodeSingleBlock(File.ReadAllBytes(file));
        }

        private static IEnumerable<RLPCollection> DecodeConsecutiveBlocks(byte[] bytes)
        {
            if (!(Nethereum.RLP.RLP.DecodeCollection(bytes) is RLPCollection stream)) yield break;
            foreach (var element in stream)
                if (element is RLPCollection block) yield return block;
        }

        private static RLPCollection DecodeSingleBlock(byte[] bytes) =>
            (RLPCollection)Nethereum.RLP.RLP.Decode(bytes);

        private static IEnumerable<string> EnumerateBlockFilesInNumericOrder(string directory) =>
            Directory.EnumerateFiles(directory, "*.rlp")
                .OrderBy(path => LeadingNumberOrLast(Path.GetFileName(path)));

        private static long LeadingNumberOrLast(string fileName)
        {
            var digits = new string(fileName.TakeWhile(char.IsDigit).ToArray());
            return digits.Length == 0 ? long.MaxValue : long.Parse(digits);
        }

        private async Task<bool> TryImportBlockAsync(RLPCollection block, CancellationToken ct)
        {
            try
            {
                var header = BlockHeaderEncoder.Current.Decode(block[0].RLPData);
                var result = await _importer.ImportAsync(
                    header,
                    DecodeTransactions(block),
                    DecodeUncles(block),
                    DecodeWithdrawals(block),
                    ct).ConfigureAwait(false);
                return result.BlockHash != null;
            }
            catch
            {
                return false;
            }
        }

        private static IList<ISignedTransaction> DecodeTransactions(RLPCollection block)
        {
            var result = new List<ISignedTransaction>();
            if (block.Count < 2 || !(block[1] is RLPCollection txList)) return result;

            foreach (var txItem in txList)
            {
                var txBytes = txItem.RLPData;
                if ((txBytes == null || txBytes.Length == 0) && txItem is RLPCollection legacyFields)
                    txBytes = ReEncodeLegacyFields(legacyFields);

                if (txBytes != null && txBytes.Length > 0)
                    result.Add(TransactionFactory.CreateTransaction(txBytes));
            }

            return result;
        }

        private static byte[] ReEncodeLegacyFields(RLPCollection fields)
        {
            var encoded = new byte[fields.Count][];
            for (var i = 0; i < fields.Count; i++)
                encoded[i] = Nethereum.RLP.RLP.EncodeElement(fields[i].RLPData);
            return Nethereum.RLP.RLP.EncodeList(encoded);
        }

        private static IList<BlockHeader> DecodeUncles(RLPCollection block)
        {
            if (block.Count < 3 || !(block[2] is RLPCollection uncleList) || uncleList.Count == 0)
                return null;

            var result = new List<BlockHeader>(uncleList.Count);
            foreach (var uncle in uncleList)
                result.Add(BlockHeaderEncoder.Current.Decode(uncle.RLPData));
            return result;
        }

        private static IList<Withdrawal> DecodeWithdrawals(RLPCollection block)
        {
            if (block.Count < 4 || !(block[3] is RLPCollection withdrawalList))
                return null;

            var withdrawals = new List<Withdrawal>(withdrawalList.Count);
            foreach (var withdrawal in withdrawalList)
                withdrawals.Add(WithdrawalEncoder.Current.Decode(withdrawal.RLPData));

            return withdrawals;
        }
    }
}
