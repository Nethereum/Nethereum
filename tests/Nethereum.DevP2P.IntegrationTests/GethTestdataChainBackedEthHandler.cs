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

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Serves eth wire-protocol read requests (GetBlockHeaders, GetBlockBodies,
    /// GetReceipts) from a pre-loaded chain.rlp. Used by the
    /// <c>devp2p rlpx eth-test</c> conformance harness.
    ///
    /// Indices it builds at load time:
    ///   - <see cref="HeadersByNumber"/>: number → header
    ///   - <see cref="HeadersByHash"/>:   keccak256(rlp(header)) → header
    ///   - <see cref="BodiesByHash"/>:    hash → (txs, uncles, withdrawals)
    /// </summary>
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

            // Genesis is not in chain.rlp (which starts at block 1). Index it
            // here at HeadersByNumber[0] when provided so block-0 lookups work
            // for sub-tests like ZeroRequestID.
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
            // Resolve the starting block (either by hash or by number).
            int startIndex;
            if (request.StartBlockHash != null && request.StartBlockHash.Length == 32)
            {
                if (!HeadersByHash.TryGetValue(request.StartBlockHash.ToHex(), out var startHeader))
                    return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
                startIndex = HeadersByNumber.IndexOf(startHeader);
            }
            else
            {
                // HeadersByNumber may have genesis at index 0; in that case
                // block N → index N. Otherwise blocks start at 1 → index N-1.
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
                    result.Add(new BlockBody());  // empty placeholder keeps response shape consistent
            }
            return Task.FromResult<IList<BlockBody>>(result);
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken ct = default)
        {
            // Empty receipts list per requested hash — passes count-equality
            // sub-tests but not content checks. Receipts are computable from
            // re-execution but not currently captured; deferred until needed.
            var result = new List<List<Receipt>>();
            for (int i = 0; i < blockHashes.Length; i++) result.Add(new List<Receipt>());
            return Task.FromResult(result);
        }

        public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken ct = default)
        {
            // chain.rlp testdata harness has no tx pool — always return empty.
            // GetPooledTransactions sub-tests use a separate pool-backed handler.
            return Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
        }

    }
}
