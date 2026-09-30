using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Model;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public static class BlockAccessListDiff
    {
        public static IReadOnlyList<string> Describe(
            List<AccountChanges> produced,
            List<AccountChanges> declared,
            int maxLines = 12)
        {
            if (declared == null)
                return new[] { "the fixture declares no access list to compare against" };
            if (produced == null)
                return new[] { $"the engine recorded none; the fixture declares {declared.Count} account(s)" };

            var lines = new List<string>();
            var ourAccounts = ByAddress(produced);
            var theirAccounts = ByAddress(declared);

            foreach (var address in theirAccounts.Keys.Where(k => !ourAccounts.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                lines.Add($"MISSING account {address} — the fixture has it, we do not{Summarise(theirAccounts[address])}");

            foreach (var address in ourAccounts.Keys.Where(k => !theirAccounts.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                lines.Add($"EXTRA account {address} — we recorded it, the fixture does not{Summarise(ourAccounts[address])}");

            foreach (var address in theirAccounts.Keys.Where(ourAccounts.ContainsKey).OrderBy(k => k, StringComparer.Ordinal))
                DescribeAccount(lines, address, ourAccounts[address], theirAccounts[address]);

            if (lines.Count == 0)
            {
                var ourOrder = produced.Select(a => Normalise(a.Address)).ToList();
                var theirOrder = declared.Select(a => Normalise(a.Address)).ToList();
                if (!ourOrder.SequenceEqual(ourOrder.OrderBy(a => a, StringComparer.Ordinal)))
                    lines.Add("ACCOUNT ORDER: our accounts are not in ascending address order");
                if (!ourOrder.SequenceEqual(theirOrder))
                    lines.Add($"ACCOUNT ORDER: ours=[{string.Join(" ", ourOrder)}] fixture=[{string.Join(" ", theirOrder)}]");
            }

            if (lines.Count <= maxLines) return lines;

            var capped = lines.Take(maxLines).ToList();
            capped.Add($"... and {lines.Count - maxLines} more difference(s)");
            return capped;
        }

        public static string Summary(List<AccountChanges> produced, List<AccountChanges> declared)
        {
            var lines = Describe(produced, declared, maxLines: 4);
            return lines.Count == 0
                ? "BAL collection is IDENTICAL to the fixture's — the divergence is in the ENCODING"
                : string.Join(" | ", lines);
        }

        private static void DescribeAccount(List<string> lines, string address, AccountChanges ours, AccountChanges theirs)
        {
            CompareSeries(lines, address, "balanceChanges",
                ours.BalanceChanges, theirs.BalanceChanges,
                c => $"idx{c.BlockAccessIndex}={c.PostBalance}");

            CompareSeries(lines, address, "nonceChanges",
                ours.NonceChanges, theirs.NonceChanges,
                c => $"idx{c.BlockAccessIndex}={c.NewNonce}");

            CompareSeries(lines, address, "codeChanges",
                ours.CodeChanges, theirs.CodeChanges,
                c => $"idx{c.BlockAccessIndex}={(c.NewCode == null ? 0 : c.NewCode.Length)}B");

            CompareSeries(lines, address, "storageReads",
                ours.StorageReads, theirs.StorageReads,
                slot => slot.ToString());

            CompareSeries(lines, address, "storageChanges",
                ours.StorageChanges, theirs.StorageChanges,
                s => $"{s.Slot}=[{string.Join(",", (s.Changes ?? new List<StorageChange>()).Select(c => $"idx{c.BlockAccessIndex}={c.PostValue}"))}]");
        }

        private static void CompareSeries<T>(
            List<string> lines, string address, string what,
            List<T> ours, List<T> theirs, Func<T, string> render)
        {
            var a = (ours ?? new List<T>()).Select(render).ToList();
            var b = (theirs ?? new List<T>()).Select(render).ToList();
            if (a.SequenceEqual(b)) return;

            var onlyOurs = a.Except(b).ToList();
            var onlyTheirs = b.Except(a).ToList();

            if (onlyOurs.Count == 0 && onlyTheirs.Count == 0)
            {
                lines.Add($"{address} {what} ORDER: ours=[{string.Join(" ", a)}] fixture=[{string.Join(" ", b)}]");
                return;
            }

            var detail = new List<string>();
            if (onlyOurs.Count > 0) detail.Add($"WE ADD [{string.Join(" ", onlyOurs)}]");
            if (onlyTheirs.Count > 0) detail.Add($"WE MISS [{string.Join(" ", onlyTheirs)}]");
            lines.Add($"{address} {what}: {string.Join(", ", detail)} (ours=[{string.Join(" ", a)}] fixture=[{string.Join(" ", b)}])");
        }

        private static string Summarise(AccountChanges account)
        {
            var parts = new List<string>();
            if (account.BalanceChanges?.Count > 0) parts.Add($"{account.BalanceChanges.Count} balance");
            if (account.NonceChanges?.Count > 0) parts.Add($"{account.NonceChanges.Count} nonce");
            if (account.CodeChanges?.Count > 0) parts.Add($"{account.CodeChanges.Count} code");
            if (account.StorageChanges?.Count > 0) parts.Add($"{account.StorageChanges.Count} storageChange");
            if (account.StorageReads?.Count > 0) parts.Add($"{account.StorageReads.Count} storageRead");
            return parts.Count == 0 ? " (touched only)" : $" ({string.Join(", ", parts)})";
        }

        private static Dictionary<string, AccountChanges> ByAddress(List<AccountChanges> list)
        {
            var map = new Dictionary<string, AccountChanges>(StringComparer.Ordinal);
            foreach (var account in list)
                map[Normalise(account.Address)] = account;
            return map;
        }

        private static string Normalise(string address)
        {
            if (string.IsNullOrEmpty(address)) return address;
            var trimmed = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address.Substring(2) : address;
            return "0x" + trimmed.ToLowerInvariant();
        }
    }
}
