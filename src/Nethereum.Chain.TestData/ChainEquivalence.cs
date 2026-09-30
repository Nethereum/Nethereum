using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Chain.TestData
{
    public static class ChainEquivalence
    {
        public static async Task<ChainStateComparison.Result> CompareAllAsync(
            ChainStores expected, ChainStores actual, int maxDifferencesReported = 10)
        {
            var result = new ChainStateComparison.Result { Subject = "chain" };

            var height = (long)await expected.Blocks.GetHeightAsync().ConfigureAwait(false);
            var actualHeight = (long)await actual.Blocks.GetHeightAsync().ConfigureAwait(false);
            result.Expected = (int)height;
            result.Actual = (int)actualHeight;

            if (height != actualHeight)
                Report(result, maxDifferencesReported, $"height {actualHeight}, expected {height}");

            await CompareCanonicalHashesAsync(expected, actual, height, result, maxDifferencesReported)
                .ConfigureAwait(false);
            await CompareStateAsync(expected, actual, result, maxDifferencesReported).ConfigureAwait(false);
            await CompareBodiesAsync(expected, actual, height, result, maxDifferencesReported)
                .ConfigureAwait(false);

            return result;
        }

        public static async Task AssertStateEquivalentAsync(
            ChainStores expected, ChainStores actual, int maxDifferencesReported = 10)
        {
            var result = new ChainStateComparison.Result { Subject = "state" };
            var height = (long)await expected.Blocks.GetHeightAsync().ConfigureAwait(false);

            await CompareCanonicalHashesAsync(expected, actual, height, result, maxDifferencesReported)
                .ConfigureAwait(false);
            await CompareStateAsync(expected, actual, result, maxDifferencesReported).ConfigureAwait(false);

            if (result.Differences.Count > 0) throw new ChainsDifferException(result);
        }

        public static async Task AssertEquivalentAsync(
            ChainStores expected, ChainStores actual, int maxDifferencesReported = 10)
        {
            var result = await CompareAllAsync(expected, actual, maxDifferencesReported).ConfigureAwait(false);
            if (result.Differences.Count > 0)
                throw new ChainsDifferException(result);
        }

        private static async Task CompareCanonicalHashesAsync(
            ChainStores expected, ChainStores actual, long height,
            ChainStateComparison.Result result, int max)
        {
            for (var n = 0L; n <= height; n++)
            {
                var theirs = await expected.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                var mine = await actual.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                if (!ByteUtil.AreEqual(theirs, mine))
                    Report(result, max, $"block {n} hash {Hex(mine)}, expected {Hex(theirs)}");
            }
        }

        private static async Task CompareStateAsync(
            ChainStores expected, ChainStores actual, ChainStateComparison.Result result, int max)
        {
            var theirAccounts = await expected.State.GetAllAccountsAsync().ConfigureAwait(false);
            var myAccounts = await actual.State.GetAllAccountsAsync().ConfigureAwait(false);

            foreach (var address in myAccounts.Keys.Where(k => !theirAccounts.ContainsKey(k)))
            {
                if (await expected.State.GetAccountAsync(address).ConfigureAwait(false) != null) continue;

                if (address == "0x0000000000000000000000000000000000000000") continue;

                var extra = myAccounts[address];
                var empty = extra.Balance.IsZero && extra.Nonce.IsZero
                            && (extra.CodeHash == null || extra.CodeHash.Length == 0
                                || ByteUtil.AreEqual(extra.CodeHash, DefaultValues.EMPTY_DATA_HASH));
                if (!empty) Report(result, max, $"{address} present but not expected (balance {extra.Balance}, nonce {extra.Nonce}, code {Hex(extra.CodeHash)})");
            }

            foreach (var entry in theirAccounts)
            {
                var address = entry.Key;
                var theirs = entry.Value;

                if (!myAccounts.TryGetValue(address, out var mine))
                    mine = await actual.State.GetAccountAsync(address).ConfigureAwait(false);

                if (mine == null)
                {
                    Report(result, max, $"{address} missing");
                    continue;
                }

                if (theirs.Balance != mine.Balance)
                    Report(result, max, $"{address} balance {mine.Balance}, expected {theirs.Balance}");
                if (theirs.Nonce != mine.Nonce)
                    Report(result, max, $"{address} nonce {mine.Nonce}, expected {theirs.Nonce}");
                if (!BytesEqual(theirs.CodeHash, mine.CodeHash))
                    Report(result, max, $"{address} code hash differs");

                if (!BytesEqual(theirs.StateRoot, mine.StateRoot))
                    Report(result, max, $"{address} storage root {Hex(mine.StateRoot)}, expected {Hex(theirs.StateRoot)}");

                await CompareStorageAsync(expected, actual, address, result, max).ConfigureAwait(false);

                if (theirs.CodeHash != null && theirs.CodeHash.Length > 0)
                {
                    var theirCode = await expected.State.GetCodeAsync(theirs.CodeHash).ConfigureAwait(false);
                    var myCode = await actual.State.GetCodeAsync(theirs.CodeHash).ConfigureAwait(false);
                    if (!BytesEqual(theirCode, myCode))
                        Report(result, max, $"{address} code bytes differ");
                }
            }
        }

        private static async Task CompareStorageAsync(
            ChainStores expected, ChainStores actual, string address,
            ChainStateComparison.Result result, int max)
        {
            var theirs = await expected.State.GetAllStorageAsync(address).ConfigureAwait(false);
            if (theirs.Count == 0) return;

            var mine = await actual.State.GetAllStorageAsync(address).ConfigureAwait(false);
            foreach (var slot in theirs)
            {
                if (!mine.TryGetValue(slot.Key, out var mineValue))
                    Report(result, max, $"{address} slot 0x{slot.Key.ToHex()} missing");
                else if (!BytesEqual(slot.Value, mineValue))
                    Report(result, max,
                        $"{address} slot 0x{slot.Key.ToHex()} = {Hex(mineValue)}, expected {Hex(slot.Value)}");
            }
        }

        private static async Task CompareBodiesAsync(
            ChainStores expected, ChainStores actual, long height,
            ChainStateComparison.Result result, int max)
        {
            for (var n = 1L; n <= height; n++)
            {
                var blockHash = await expected.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                if (blockHash == null) continue;

                var theirTxs = await expected.Transactions.GetHashesByBlockHashAsync(blockHash).ConfigureAwait(false);
                var myTxs = await actual.Transactions.GetHashesByBlockHashAsync(blockHash).ConfigureAwait(false);
                if ((theirTxs?.Count ?? 0) != (myTxs?.Count ?? 0))
                    Report(result, max, $"block {n} has {myTxs?.Count ?? 0} transactions, expected {theirTxs?.Count ?? 0}");

                var theirReceipts = await expected.Receipts.GetByBlockNumberAsync(n).ConfigureAwait(false);
                var myReceipts = await actual.Receipts.GetByBlockNumberAsync(n).ConfigureAwait(false);
                if (theirReceipts.Count != myReceipts.Count)
                {
                    Report(result, max, $"block {n} has {myReceipts.Count} receipts, expected {theirReceipts.Count}");
                }
                else
                {
                    for (var i = 0; i < theirReceipts.Count; i++)
                    {
                        if (!BytesEqual(theirReceipts[i].PostStateOrStatus, myReceipts[i].PostStateOrStatus))
                            Report(result, max, $"block {n} receipt {i} status differs");
                        if (theirReceipts[i].CumulativeGasUsed != myReceipts[i].CumulativeGasUsed)
                            Report(result, max, $"block {n} receipt {i} cumulative gas differs");
                        if (!BytesEqual(theirReceipts[i].Bloom, myReceipts[i].Bloom))
                            Report(result, max, $"block {n} receipt {i} bloom differs");
                    }
                }

                var theirLogs = await expected.Logs.GetLogsByBlockNumberAsync(n).ConfigureAwait(false);
                var myLogs = await actual.Logs.GetLogsByBlockNumberAsync(n).ConfigureAwait(false);
                if (theirLogs.Count != myLogs.Count)
                {
                    Report(result, max, $"block {n} has {myLogs.Count} logs, expected {theirLogs.Count}");
                    continue;
                }

                for (var i = 0; i < theirLogs.Count; i++)
                {
                    if (!string.Equals(theirLogs[i].Address, myLogs[i].Address, StringComparison.OrdinalIgnoreCase))
                        Report(result, max, $"block {n} log {i} address differs");
                    if (!BytesEqual(theirLogs[i].Data, myLogs[i].Data))
                        Report(result, max, $"block {n} log {i} data {Hex(myLogs[i].Data)}, expected {Hex(theirLogs[i].Data)}");
                    if (!TopicsEqual(theirLogs[i].Topics, myLogs[i].Topics))
                        Report(result, max, $"block {n} log {i} topics differ");
                    if (theirLogs[i].LogIndex != myLogs[i].LogIndex)
                        Report(result, max, $"block {n} log {i} index differs");
                    if (theirLogs[i].TransactionIndex != myLogs[i].TransactionIndex)
                        Report(result, max, $"block {n} log {i} transaction index differs");
                }

                await CompareBlockAccessListAsync(expected, actual, blockHash, n, result, max).ConfigureAwait(false);
            }
        }

        private static async Task CompareBlockAccessListAsync(
            ChainStores expected, ChainStores actual, byte[] blockHash, long n,
            ChainStateComparison.Result result, int max)
        {
            var theirsRlp = await expected.BlockAccessLists.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
            var mineRlp = await actual.BlockAccessLists.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
            if (theirsRlp == null && mineRlp == null) return;
            if (theirsRlp == null)
            {
                Report(result, max, $"block {n} has a block access list but none was expected");
                return;
            }
            if (mineRlp == null)
            {
                Report(result, max, $"block {n} block access list missing");
                return;
            }

            var theirs = BlockAccessListRLPEncoder.Current.Decode(theirsRlp);
            var mine = BlockAccessListRLPEncoder.Current.Decode(mineRlp);
            if (theirs.Count != mine.Count)
            {
                Report(result, max, $"block {n} block access list has {mine.Count} accounts, expected {theirs.Count}");
                return;
            }

            for (var i = 0; i < theirs.Count; i++)
                CompareAccountChanges(theirs[i], mine[i], n, i, result, max);
        }

        private static void CompareAccountChanges(
            AccountChanges theirs, AccountChanges mine, long n, int accountIndex,
            ChainStateComparison.Result result, int max)
        {
            if (!string.Equals(theirs.Address, mine.Address, StringComparison.OrdinalIgnoreCase))
                Report(result, max, $"block {n} block access list account {accountIndex} address differs");

            CompareStorageChanges(theirs.StorageChanges, mine.StorageChanges, n, accountIndex, result, max);
            CompareStorageReads(theirs.StorageReads, mine.StorageReads, n, accountIndex, result, max);

            CompareIndexedChanges(theirs.BalanceChanges, mine.BalanceChanges, c => c.BlockAccessIndex,
                (a, b) => a.PostBalance == b.PostBalance, n, accountIndex, "balance change", result, max);
            CompareIndexedChanges(theirs.NonceChanges, mine.NonceChanges, c => c.BlockAccessIndex,
                (a, b) => a.NewNonce == b.NewNonce, n, accountIndex, "nonce change", result, max);
            CompareIndexedChanges(theirs.CodeChanges, mine.CodeChanges, c => c.BlockAccessIndex,
                (a, b) => BytesEqual(a.NewCode, b.NewCode), n, accountIndex, "code change", result, max);
        }

        private static void CompareStorageChanges(
            List<SlotChanges> theirs, List<SlotChanges> mine, long n, int accountIndex,
            ChainStateComparison.Result result, int max)
        {
            if ((theirs?.Count ?? 0) != (mine?.Count ?? 0))
            {
                Report(result, max,
                    $"block {n} account {accountIndex} has {mine?.Count ?? 0} storage-changed slots, expected {theirs?.Count ?? 0}");
                return;
            }

            for (var s = 0; s < theirs.Count; s++)
            {
                if (theirs[s].Slot != mine[s].Slot)
                {
                    Report(result, max, $"block {n} account {accountIndex} storage slot {s} address differs");
                    continue;
                }

                CompareIndexedChanges(theirs[s].Changes, mine[s].Changes, c => c.BlockAccessIndex,
                    (a, b) => a.PostValue == b.PostValue, n, accountIndex, $"slot {s} change", result, max);
            }
        }

        private static void CompareStorageReads(
            List<EvmUInt256> theirs, List<EvmUInt256> mine, long n, int accountIndex,
            ChainStateComparison.Result result, int max)
        {
            if ((theirs?.Count ?? 0) != (mine?.Count ?? 0))
            {
                Report(result, max,
                    $"block {n} account {accountIndex} has {mine?.Count ?? 0} storage reads, expected {theirs?.Count ?? 0}");
                return;
            }

            for (var r = 0; r < theirs.Count; r++)
                if (theirs[r] != mine[r])
                    Report(result, max, $"block {n} account {accountIndex} storage read {r} differs");
        }

        private static void CompareIndexedChanges<T>(
            List<T> theirs, List<T> mine, Func<T, ulong> index, Func<T, T, bool> valueEquals,
            long n, int accountIndex, string label,
            ChainStateComparison.Result result, int max)
        {
            if ((theirs?.Count ?? 0) != (mine?.Count ?? 0))
            {
                Report(result, max,
                    $"block {n} account {accountIndex} has {mine?.Count ?? 0} {label}s, expected {theirs?.Count ?? 0}");
                return;
            }

            for (var k = 0; k < theirs.Count; k++)
            {
                if (index(theirs[k]) != index(mine[k]))
                    Report(result, max, $"block {n} account {accountIndex} {label} {k} index differs");
                else if (!valueEquals(theirs[k], mine[k]))
                    Report(result, max, $"block {n} account {accountIndex} {label} {k} value differs");
            }
        }

        private static bool TopicsEqual(IList<byte[]> theirs, IList<byte[]> mine)
        {
            if ((theirs?.Count ?? 0) != (mine?.Count ?? 0)) return false;
            for (var i = 0; i < (theirs?.Count ?? 0); i++)
                if (!BytesEqual(theirs[i], mine[i])) return false;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if ((a == null || a.Length == 0) && (b == null || b.Length == 0)) return true;
            return ByteUtil.AreEqual(a, b);
        }

        private static void Report(ChainStateComparison.Result result, int max, string difference)
        {
            if (result.Differences.Count < max) result.Differences.Add(difference);
        }

        private static string Hex(byte[] value) => value == null ? "<null>" : "0x" + value.ToHex();
    }

    public sealed class ChainsDifferException : Exception
    {
        public ChainsDifferException(ChainStateComparison.Result result)
            : base(result.ToString())
        {
            Result = result;
        }

        public ChainStateComparison.Result Result { get; }
    }
}
