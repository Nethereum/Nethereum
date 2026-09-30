using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class GethTestdataChainBackedEthHandler : IEth68RequestHandler
    {
        public List<BlockHeader> HeadersByNumber { get; } = new();
        public Dictionary<string, BlockHeader> HeadersByHash { get; } = new();
        public Dictionary<string, BlockBody> BodiesByHash { get; } = new();
        public BlockHeader Head => HeadersByNumber[^1];

        public static GethTestdataChainBackedEthHandler Load(string chainRlpPath, BlockHeader genesisHeader = null)
        {
            var bytes = File.ReadAllBytes(chainRlpPath);
            var handler = new GethTestdataChainBackedEthHandler();
            var hashProvider = new Sha3KeccackHashProvider();

            if (genesisHeader != null)
            {
                var genesisEncoded = new BlockHeaderEncoder().Encode(genesisHeader);
                var genesisHash = hashProvider.ComputeHash(genesisEncoded);
                handler.HeadersByNumber.Add(genesisHeader);
                handler.HeadersByHash[genesisHash.ToHex()] = genesisHeader;
                handler.BodiesByHash[genesisHash.ToHex()] = new BlockBody();
            }

            int pos = 0;
            while (pos < bytes.Length)
            {
                var blockColl = (RLPCollection)RLP.RLP.DecodeFirstElement(bytes, pos);
                int consumed = Helpers.RlpStreamHelpers.GetRlpItemLength(bytes, pos);
                var headerColl = (RLPCollection)blockColl[0];
                var headerEncoded = Helpers.RlpStreamHelpers.ReEncodeAsList(headerColl);
                var header = new BlockHeaderEncoder().Decode(headerEncoded);
                var hash = hashProvider.ComputeHash(headerEncoded);

                var txs = new List<ISignedTransaction>();
                foreach (var txItem in (RLPCollection)blockColl[1])
                {
                    byte[] txBytes = txItem is RLPCollection c ? Helpers.RlpStreamHelpers.ReEncodeAsList(c) : txItem.RLPData;
                    txs.Add(TransactionFactory.CreateTransaction(txBytes));
                }
                var uncles = new List<BlockHeader>();
                foreach (var u in (RLPCollection)blockColl[2])
                    uncles.Add(new BlockHeaderEncoder().Decode(Helpers.RlpStreamHelpers.ReEncodeAsList((RLPCollection)u)));
                var withdrawals = new List<Withdrawal>();
                if (blockColl.Count >= 4 && blockColl[3] is RLPCollection wList)
                {
                    foreach (var wItem in wList)
                    {
                        var wColl = (RLPCollection)wItem;
                        withdrawals.Add(new Withdrawal
                        {
                            Index = (ulong)wColl[0].RLPData.ToLongFromRLPDecoded(),
                            ValidatorIndex = (ulong)wColl[1].RLPData.ToLongFromRLPDecoded(),
                            Address = wColl[2].RLPData,
                            AmountInGwei = (ulong)wColl[3].RLPData.ToLongFromRLPDecoded()
                        });
                    }
                }

                handler.HeadersByNumber.Add(header);
                handler.HeadersByHash[hash.ToHex()] = header;
                handler.BodiesByHash[hash.ToHex()] = new BlockBody
                {
                    Transactions = txs,
                    Uncles = uncles,
                    Withdrawals = withdrawals.Count > 0 ? withdrawals : null
                };
                pos += consumed;
            }
            return handler;
        }

        public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken ct = default)
        {
            int startIndex;
            if (request.StartBlockHash != null && request.StartBlockHash.Length == 32)
            {
                if (!HeadersByHash.TryGetValue(request.StartBlockHash.ToHex(), out var startHeader))
                    return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
                startIndex = HeadersByNumber.IndexOf(startHeader);
            }
            else
            {
                bool hasGenesis = HeadersByNumber.Count > 0 && (long)HeadersByNumber[0].BlockNumber == 0;
                startIndex = hasGenesis ? (int)request.StartBlock : (int)request.StartBlock - 1;
            }

            var result = new List<BlockHeader>();
            int step = (int)request.Skip + 1;
            int direction = request.Reverse ? -1 : 1;
            for (ulong i = 0; i < request.Limit; i++)
            {
                int idx = startIndex + direction * step * (int)i;
                if (idx < 0 || idx >= HeadersByNumber.Count) break;
                result.Add(HeadersByNumber[idx]);
            }
            return Task.FromResult<IList<BlockHeader>>(result);
        }

        public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken ct = default)
        {
            var result = new List<BlockBody>();
            foreach (var h in blockHashes)
            {
                if (BodiesByHash.TryGetValue(h.ToHex(), out var body))
                    result.Add(body);
                else
                    result.Add(new BlockBody());
            }
            return Task.FromResult<IList<BlockBody>>(result);
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken ct = default)
        {
            var result = new List<List<Receipt>>();
            for (int i = 0; i < blockHashes.Length; i++) result.Add(new List<Receipt>());
            return Task.FromResult(result);
        }

        public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken ct = default)
        {
            var result = new List<List<Receipt>>();
            for (int i = 0; i < blockHashes.Length; i++) result.Add(new List<Receipt>());
            return Task.FromResult(new Receipts70Result(result, false));
        }

        public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken ct = default)
        {
            return Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
        }

        public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken ct = default)
        {
            return Task.FromResult(new List<byte[]>());
        }

    }
}
