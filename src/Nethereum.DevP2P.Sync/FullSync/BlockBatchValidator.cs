using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public static class BlockBatchValidator
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        public static bool ValidateParentChain(
            IList<BlockHeader> headers,
            IList<byte[]> hashes,
            byte[] anchorHash,
            out int brokenAt)
        {
            if (anchorHash != null && !ByteUtil.AreEqual(headers[0].ParentHash, anchorHash))
            {
                brokenAt = 0;
                return false;
            }
            for (int i = 1; i < headers.Count; i++)
            {
                if (!ByteUtil.AreEqual(headers[i].ParentHash, hashes[i - 1]))
                {
                    brokenAt = i;
                    return false;
                }
            }
            brokenAt = -1;
            return true;
        }

        public static bool ValidateBodies(
            IList<BlockHeader> headers,
            IList<BlockBody> bodies,
            int paired,
            IBlockRootsProvider rootsProvider,
            BodyMismatchHandler onMismatch = null)
        {
            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var body = bodies[i];
                var txs = body?.Transactions ?? new List<ISignedTransaction>();
                var uncles = body?.Uncles ?? new List<BlockHeader>();

                var computedTxRoot = rootsProvider.CalculateTransactionsRoot(txs);
                var txRootOk = ByteUtil.AreEqual(computedTxRoot, header.TransactionsHash);

                var computedUnclesHash = uncles.Count == 0
                    ? EmptyUnclesHash
                    : ComputeUnclesHash(uncles);
                var unclesOk = ByteUtil.AreEqual(computedUnclesHash, header.UnclesHash);

                byte[] computedWithdrawalsRoot = null;
                bool withdrawalsOk = true;
                if (header.WithdrawalsRoot != null)
                {
                    var withdrawals = body?.Withdrawals;
                    if (withdrawals == null)
                    {
                        withdrawalsOk = false;
                    }
                    else
                    {
                        computedWithdrawalsRoot = rootsProvider.CalculateWithdrawalsRoot(withdrawals);
                        withdrawalsOk = ByteUtil.AreEqual(computedWithdrawalsRoot, header.WithdrawalsRoot);
                    }
                }
                else if (body?.Withdrawals != null && body.Withdrawals.Count > 0)
                {
                    withdrawalsOk = false;
                }

                if (txRootOk && unclesOk && withdrawalsOk) continue;

                onMismatch?.Invoke(new BodyMismatch(
                    Index: i,
                    Header: header,
                    Transactions: txs,
                    Uncles: uncles,
                    ComputedTxRoot: computedTxRoot,
                    TxRootOk: txRootOk,
                    ComputedUnclesHash: computedUnclesHash,
                    UnclesOk: unclesOk,
                    ComputedWithdrawalsRoot: computedWithdrawalsRoot,
                    WithdrawalsOk: withdrawalsOk));
                return false;
            }
            return true;
        }

        public static bool ValidateReceipts(
            IList<BlockHeader> headers,
            IList<List<Receipt>> receipts,
            int paired,
            IBlockRootsProvider rootsProvider,
            Func<BlockHeader, bool> shouldValidateBlock = null,
            ReceiptMismatchHandler onMismatch = null)
        {
            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var blockReceipts = receipts[i] ?? new List<Receipt>();

                if (shouldValidateBlock != null && !shouldValidateBlock(header)) continue;

                var computedRoot = blockReceipts.Count == 0
                    ? EmptyTrieRoot
                    : rootsProvider.CalculateReceiptsRoot(blockReceipts);

                if (ByteUtil.AreEqual(computedRoot, header.ReceiptHash)) continue;

                onMismatch?.Invoke(new ReceiptMismatch(
                    Index: i,
                    Header: header,
                    BlockReceipts: blockReceipts,
                    ComputedRoot: computedRoot));
                return false;
            }
            return true;
        }

        public static List<BlockBody> RealignBodies(
            IList<BlockHeader> headers,
            IList<BlockBody> bodies,
            IBlockRootsProvider rootsProvider,
            out int unmatchedFromIndex)
        {
            bool anyShanghai = false;
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].WithdrawalsRoot != null) { anyShanghai = true; break; }
            }

            var bag = new Dictionary<string, Queue<BlockBody>>(bodies.Count);
            foreach (var body in bodies)
            {
                var txs = body?.Transactions ?? new List<ISignedTransaction>();
                var uncles = body?.Uncles ?? new List<BlockHeader>();
                var txRoot = rootsProvider.CalculateTransactionsRoot(txs);
                var unclesHash = uncles.Count == 0 ? EmptyUnclesHash : ComputeUnclesHash(uncles);
                string key;
                if (anyShanghai)
                {
                    var withdrawalsRoot = body?.Withdrawals != null
                        ? rootsProvider.CalculateWithdrawalsRoot(body.Withdrawals)
                        : null;
                    key = txRoot.ToHex() + ":" + unclesHash.ToHex() + ":" + (withdrawalsRoot != null ? withdrawalsRoot.ToHex() : "");
                }
                else
                {
                    key = txRoot.ToHex() + ":" + unclesHash.ToHex();
                }
                if (!bag.TryGetValue(key, out var q))
                {
                    q = new Queue<BlockBody>();
                    bag[key] = q;
                }
                q.Enqueue(body);
            }

            var realigned = new List<BlockBody>(headers.Count);
            unmatchedFromIndex = headers.Count;
            for (int i = 0; i < headers.Count; i++)
            {
                string key;
                if (anyShanghai)
                {
                    var wr = headers[i].WithdrawalsRoot;
                    key = headers[i].TransactionsHash.ToHex() + ":" + headers[i].UnclesHash.ToHex() + ":" + (wr != null ? wr.ToHex() : "");
                }
                else
                {
                    key = headers[i].TransactionsHash.ToHex() + ":" + headers[i].UnclesHash.ToHex();
                }
                if (!bag.TryGetValue(key, out var q) || q.Count == 0)
                {
                    unmatchedFromIndex = i;
                    break;
                }
                realigned.Add(q.Dequeue());
            }

            return realigned;
        }

        public static List<List<Receipt>> RealignReceipts(
            IList<BlockHeader> headers,
            IList<List<Receipt>> receipts,
            int paired,
            IBlockRootsProvider rootsProvider,
            Func<BlockHeader, bool> isPostByzantium,
            out int unmatchedFromIndex)
        {
            var bag = new Dictionary<string, Queue<int>>(receipts.Count);
            for (int j = 0; j < receipts.Count; j++)
            {
                var list = receipts[j] ?? new List<Receipt>();
                var root = list.Count == 0 ? EmptyTrieRoot : rootsProvider.CalculateReceiptsRoot(list);
                var key = root.ToHex();
                if (!bag.TryGetValue(key, out var q))
                {
                    q = new Queue<int>();
                    bag[key] = q;
                }
                q.Enqueue(j);
            }

            var unconsumed = new SortedSet<int>();
            for (int j = 0; j < receipts.Count; j++) unconsumed.Add(j);

            var realigned = new List<List<Receipt>>(paired);
            unmatchedFromIndex = paired;

            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                bool postByzantium = isPostByzantium == null || isPostByzantium(header);

                int matchedIdx = -1;
                if (postByzantium)
                {
                    var key = header.ReceiptHash.ToHex();
                    if (bag.TryGetValue(key, out var q) && q.Count > 0)
                    {
                        matchedIdx = q.Dequeue();
                    }
                }

                if (matchedIdx < 0)
                {
                    if (unconsumed.Count == 0)
                    {
                        unmatchedFromIndex = i;
                        break;
                    }
                    matchedIdx = unconsumed.Min;
                }

                unconsumed.Remove(matchedIdx);
                realigned.Add(receipts[matchedIdx] ?? new List<Receipt>());
            }

            return realigned;
        }

        private static byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            var keccak = new Sha3Keccack();
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return keccak.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        public sealed record BodyMismatch(
            int Index,
            BlockHeader Header,
            IList<ISignedTransaction> Transactions,
            IList<BlockHeader> Uncles,
            byte[] ComputedTxRoot,
            bool TxRootOk,
            byte[] ComputedUnclesHash,
            bool UnclesOk,
            byte[] ComputedWithdrawalsRoot = null,
            bool WithdrawalsOk = true);

        public sealed record ReceiptMismatch(
            int Index,
            BlockHeader Header,
            IList<Receipt> BlockReceipts,
            byte[] ComputedRoot);

        public delegate void BodyMismatchHandler(BodyMismatch mismatch);

        public delegate void ReceiptMismatchHandler(ReceiptMismatch mismatch);
    }
}
