using System;
using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public static class BlockBatchValidator
    {
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

        public static bool ValidateHeadersByHashReply(IList<BlockHeader> headers, byte[] startHash)
        {
            if (headers.Count == 0) return true;
            if (!BlockHashCalculator.HashesTo(headers[0], startHash)) return false;
            if (headers.Count == 1) return true;

            var hashes = new byte[headers.Count][];
            for (int i = 0; i < headers.Count; i++) hashes[i] = BlockHashCalculator.ForHeader(headers[i]);
            return ValidateParentChain(headers, hashes, null, out _);
        }

        internal static bool ValidateBackwardParentChain(
            IList<BlockHeader> headers, IList<byte[]> hashes, out int brokenAt)
        {
            for (int i = 0; i < headers.Count - 1; i++)
            {
                if (!ByteUtil.AreEqual(headers[i].ParentHash, hashes[i + 1]))
                {
                    brokenAt = i;
                    return false;
                }
            }
            brokenAt = -1;
            return true;
        }

        internal static bool HeadersAreContiguous(IList<BlockHeader> headers, ulong firstBlock, bool descending)
        {
            if (!NumberIs(headers[0].BlockNumber, firstBlock)) return false;
            for (int i = 1; i < headers.Count; i++)
            {
                var prev = headers[i - 1].BlockNumber;
                if (!prev.FitsInULong) return false;
                var previous = (ulong)prev;
                if (descending ? previous == 0 : previous == ulong.MaxValue) return false;
                if (!NumberIs(headers[i].BlockNumber, descending ? previous - 1 : previous + 1)) return false;
            }
            return true;
        }

        internal static bool ValidateHeadersByNumberReply(
            IList<BlockHeader> headers, ulong startBlock, ulong limit, bool reverse)
            => headers.Count == 0
               || ((ulong)headers.Count <= limit && HeadersAreContiguous(headers, startBlock, reverse));

        private static bool NumberIs(EvmUInt256 number, ulong expected)
            => number.FitsInULong && (ulong)number == expected;

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

                var computedUnclesHash = ComputeUnclesHash(uncles);
                var unclesOk = ByteUtil.AreEqual(computedUnclesHash, header.UnclesHash);

                var withdrawalsOk = CheckWithdrawals(header, body, rootsProvider, out var computedWithdrawalsRoot);

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

        private static bool CheckWithdrawals(
            BlockHeader header,
            BlockBody body,
            IBlockRootsProvider rootsProvider,
            out byte[] computedWithdrawalsRoot)
        {
            computedWithdrawalsRoot = null;
            if (header.WithdrawalsRoot == null)
                return !(body?.Withdrawals != null && body.Withdrawals.Count > 0);

            var withdrawals = body?.Withdrawals;
            if (withdrawals == null) return false;

            computedWithdrawalsRoot = rootsProvider.CalculateWithdrawalsRoot(withdrawals);
            return ByteUtil.AreEqual(computedWithdrawalsRoot, header.WithdrawalsRoot);
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

                var computedRoot = ComputeReceiptsRoot(blockReceipts, rootsProvider);

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
            var includeWithdrawals = AnyHeaderHasWithdrawalsRoot(headers);
            var bag = BucketBodiesByContent(bodies, rootsProvider, includeWithdrawals);

            var realigned = new List<BlockBody>(headers.Count);
            unmatchedFromIndex = headers.Count;
            for (int i = 0; i < headers.Count; i++)
            {
                var key = ContentKeyOf(headers[i], includeWithdrawals);
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
            var bag = BucketReceiptsByRoot(receipts, rootsProvider);

            var unconsumed = new SortedSet<int>();
            for (int j = 0; j < receipts.Count; j++) unconsumed.Add(j);

            var realigned = new List<List<Receipt>>(paired);
            unmatchedFromIndex = paired;

            for (int i = 0; i < paired; i++)
            {
                int matchedIdx = DequeueByReceiptRoot(bag, headers[i], isPostByzantium);

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

        private static int DequeueByReceiptRoot(
            Dictionary<string, Queue<int>> bag,
            BlockHeader header,
            Func<BlockHeader, bool> isPostByzantium)
        {
            bool postByzantium = isPostByzantium == null || isPostByzantium(header);
            if (!postByzantium) return -1;
            return bag.TryGetValue(header.ReceiptHash.ToHex(), out var q) && q.Count > 0 ? q.Dequeue() : -1;
        }

        internal static bool AnyHeaderHasWithdrawalsRoot(IList<BlockHeader> headers)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].WithdrawalsRoot != null) return true;
            }
            return false;
        }

        internal static string ContentKeyOf(BlockHeader header, bool includeWithdrawals)
        {
            var key = header.TransactionsHash.ToHex() + ":" + header.UnclesHash.ToHex();
            if (!includeWithdrawals) return key;
            var withdrawalsRoot = header.WithdrawalsRoot;
            return key + ":" + (withdrawalsRoot != null ? withdrawalsRoot.ToHex() : "");
        }

        internal static Dictionary<string, Queue<BlockBody>> BucketBodiesByContent(
            IList<BlockBody> bodies,
            IBlockRootsProvider roots,
            bool includeWithdrawals)
        {
            var bag = new Dictionary<string, Queue<BlockBody>>(bodies.Count);
            foreach (var body in bodies)
            {
                var key = ContentKeyOf(body, roots, includeWithdrawals);
                if (!bag.TryGetValue(key, out var q))
                {
                    q = new Queue<BlockBody>();
                    bag[key] = q;
                }
                q.Enqueue(body);
            }
            return bag;
        }

        private static string ContentKeyOf(BlockBody body, IBlockRootsProvider roots, bool includeWithdrawals)
        {
            var txs = body?.Transactions ?? new List<ISignedTransaction>();
            var uncles = body?.Uncles ?? new List<BlockHeader>();
            var key = roots.CalculateTransactionsRoot(txs).ToHex() + ":" + ComputeUnclesHash(uncles).ToHex();
            if (!includeWithdrawals) return key;
            var withdrawalsRoot = body?.Withdrawals != null
                ? roots.CalculateWithdrawalsRoot(body.Withdrawals)
                : null;
            return key + ":" + (withdrawalsRoot != null ? withdrawalsRoot.ToHex() : "");
        }

        internal static byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            if (uncles.Count == 0) return DefaultValues.EMPTY_UNCLES_HASH;
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return Sha3Keccack.Current.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        internal static byte[] ComputeReceiptsRoot(IList<Receipt> receipts, IBlockRootsProvider roots)
        {
            return receipts.Count == 0
                ? DefaultValues.EMPTY_TRIE_HASH
                : roots.CalculateReceiptsRoot(receipts);
        }

        internal static Dictionary<string, Queue<int>> BucketReceiptsByRoot(
            IList<List<Receipt>> receipts,
            IBlockRootsProvider roots)
        {
            var bag = new Dictionary<string, Queue<int>>(receipts.Count);
            for (int j = 0; j < receipts.Count; j++)
            {
                var list = receipts[j] ?? new List<Receipt>();
                var key = ComputeReceiptsRoot(list, roots).ToHex();
                if (!bag.TryGetValue(key, out var q))
                {
                    q = new Queue<int>();
                    bag[key] = q;
                }
                q.Enqueue(j);
            }
            return bag;
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
