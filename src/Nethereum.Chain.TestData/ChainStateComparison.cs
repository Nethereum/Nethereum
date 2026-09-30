using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Chain.TestData
{
    public static class ChainStateComparison
    {
        public sealed class Result
        {
            public string Subject { get; set; }
            public int Expected { get; set; }
            public int Actual { get; set; }
            public List<string> Differences { get; } = new List<string>();

            public bool Matches => Differences.Count == 0 && Expected == Actual;

            public override string ToString()
            {
                if (Matches) return $"{Subject}: {Actual} entr(ies), all matching";

                var detail = Differences.Count == 0
                    ? string.Empty
                    : " :: " + string.Join(", ", Differences.Take(5));

                return $"{Subject}: expected {Expected}, got {Actual}, {Differences.Count} difference(s){detail}";
            }
        }

        public static async Task<Result> CompareStorageAsync(
            IChainStoreBundle expected, IChainStoreBundle actual, string contract,
            int maxDifferencesReported = 5)
        {
            var address = contract.ToLowerInvariant();
            var theirs = ToHexMap(await expected.State.GetAllStorageAsync(address).ConfigureAwait(false));
            var mine = ToHexMap(await actual.State.GetAllStorageAsync(address).ConfigureAwait(false));

            var result = new Result
            {
                Subject = $"storage of {address}",
                Expected = theirs.Count,
                Actual = mine.Count
            };

            foreach (var entry in theirs)
            {
                if (result.Differences.Count >= maxDifferencesReported) break;

                if (!mine.TryGetValue(entry.Key, out var value))
                    result.Differences.Add($"slot {entry.Key} missing");
                else if (value != entry.Value)
                    result.Differences.Add($"slot {entry.Key} = {value}, expected {entry.Value}");
            }

            return result;
        }

        public static async Task<Result> CompareAccountsAsync(
            IChainStoreBundle expected, IChainStoreBundle actual, IEnumerable<string> addresses,
            int maxDifferencesReported = 5)
        {
            var result = new Result { Subject = "accounts" };

            foreach (var raw in addresses)
            {
                var address = raw.ToLowerInvariant();
                result.Expected++;

                var theirs = await expected.State.GetAccountAsync(address).ConfigureAwait(false);
                var mine = await actual.State.GetAccountAsync(address).ConfigureAwait(false);

                if (mine == null)
                {
                    if (result.Differences.Count < maxDifferencesReported)
                        result.Differences.Add($"{address} missing");
                    continue;
                }

                result.Actual++;
                if (result.Differences.Count >= maxDifferencesReported) continue;

                if (theirs.Balance != mine.Balance)
                    result.Differences.Add($"{address} balance {mine.Balance}, expected {theirs.Balance}");
                else if (theirs.Nonce != mine.Nonce)
                    result.Differences.Add($"{address} nonce {mine.Nonce}, expected {theirs.Nonce}");
                else if (!ByteUtil.AreEqual(theirs.CodeHash, mine.CodeHash))
                    result.Differences.Add($"{address} code hash differs");
                else if (theirs.CodeHash != null)
                {
                    var theirCode = await expected.State.GetCodeAsync(theirs.CodeHash).ConfigureAwait(false);
                    var myCode = await actual.State.GetCodeAsync(mine.CodeHash).ConfigureAwait(false);
                    if (!ByteUtil.AreEqual(theirCode, myCode))
                        result.Differences.Add($"{address} code bytes differ");
                }
            }

            return result;
        }

        public static async Task<Result> CompareBlocksAsync(
            IChainStoreBundle expected, IChainStoreBundle actual,
            BigInteger fromBlock, BigInteger toBlock, int maxDifferencesReported = 5)
        {
            var result = new Result { Subject = $"blocks {fromBlock}..{toBlock}" };

            for (var number = fromBlock; number <= toBlock; number++)
            {
                result.Expected++;

                var theirs = await expected.Blocks.GetHashByNumberAsync(number).ConfigureAwait(false);
                var mine = await actual.Blocks.GetHashByNumberAsync(number).ConfigureAwait(false);

                if (mine == null)
                {
                    if (result.Differences.Count < maxDifferencesReported)
                        result.Differences.Add($"block {number} missing");
                    continue;
                }

                result.Actual++;
                if (result.Differences.Count < maxDifferencesReported && !ByteUtil.AreEqual(theirs, mine))
                    result.Differences.Add($"block {number} hash {mine.ToHex()}, expected {theirs?.ToHex()}");
            }

            return result;
        }

        private static Dictionary<string, string> ToHexMap(Dictionary<byte[], byte[]> storage)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (storage == null) return map;

            foreach (var entry in storage)
                map[entry.Key.ToHex()] = entry.Value == null ? string.Empty : entry.Value.ToHex();

            return map;
        }
    }
}
