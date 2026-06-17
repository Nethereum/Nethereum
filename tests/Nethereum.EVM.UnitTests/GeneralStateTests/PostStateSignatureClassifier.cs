using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Account-by-account post-state diff between our impl's
    /// <see cref="ExecutionStateService"/> and geth t8n's authoritative
    /// alloc output. Emits one <see cref="AccountFieldDiff"/> per
    /// divergence and labels the WHOLE failure with a
    /// <see cref="PostStateSignature"/> chosen from a small set of
    /// canonical shapes ("extra account in ours", "coinbase balance
    /// differs by N×gasUsed", "storage at SUICIDE contract differs", …).
    ///
    /// The point: a failing test labelled <c>POST_EVM_DIVERGENCE</c> by
    /// <see cref="LegacyFailureClassifier"/> becomes labelled
    /// <c>RECIPIENT_OF_SELFDESTRUCT_BALANCE_DIFF</c> here — and 13 such
    /// labels across a category usually mean ONE missing rule, not 13.
    /// </summary>
    public class PostStateSignatureClassifier
    {
        public PostStateComparison Compare(
            Dictionary<string, AccountExecutionState> ours,
            Dictionary<string, GethPostStateAccount> theirs,
            string coinbaseAddress,
            string senderAddress)
        {
            var result = new PostStateComparison
            {
                CoinbaseAddress = coinbaseAddress?.ToLowerInvariant(),
                SenderAddress = senderAddress?.ToLowerInvariant()
            };

            // Filter our side through the same skipPhantomAccounts rule the
            // runner uses for trie computation, so the diff matches what
            // the state root actually saw.
            var oursFiltered = new Dictionary<string, AccountSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ours)
            {
                var a = kvp.Value;
                if (a.Balance.InitialChainBalance == null && a.Nonce == null && !a.IsNewContract
                    && a.Code == null && a.Storage.Count == 0 && a.Balance.GetTotalBalance().IsZero)
                    continue;
                oursFiltered[kvp.Key.ToLowerInvariant()] = new AccountSnapshot
                {
                    Balance = a.Balance.GetTotalBalance().ToBigInteger(),
                    Nonce = (a.Nonce ?? EvmUInt256.Zero).ToBigInteger(),
                    Code = a.Code ?? new byte[0],
                    Storage = a.Storage.ToDictionary(s => s.Key.ToBigInteger(), s => s.Value ?? new byte[0])
                };
            }
            var theirsLower = theirs.ToDictionary(
                kvp => kvp.Key.ToLowerInvariant(),
                kvp => new AccountSnapshot
                {
                    Balance = string.IsNullOrEmpty(kvp.Value.Balance) ? BigInteger.Zero : kvp.Value.Balance.HexToBigInteger(false),
                    Nonce = string.IsNullOrEmpty(kvp.Value.Nonce) ? BigInteger.Zero : kvp.Value.Nonce.HexToBigInteger(false),
                    Code = string.IsNullOrEmpty(kvp.Value.Code) || kvp.Value.Code == "0x" ? new byte[0] : kvp.Value.Code.HexToByteArray(),
                    Storage = kvp.Value.Storage.ToDictionary(
                        s => s.Key.HexToBigInteger(false),
                        s => s.Value?.HexToByteArray() ?? new byte[0])
                },
                StringComparer.OrdinalIgnoreCase);

            var allAddresses = new HashSet<string>(oursFiltered.Keys, StringComparer.OrdinalIgnoreCase);
            allAddresses.UnionWith(theirsLower.Keys);

            foreach (var addr in allAddresses)
            {
                var hasOurs = oursFiltered.TryGetValue(addr, out var ourAcct);
                var hasTheirs = theirsLower.TryGetValue(addr, out var theirAcct);

                if (hasOurs && !hasTheirs)
                {
                    result.Diffs.Add(new AccountFieldDiff { Address = addr, Field = "EXISTENCE", GethValue = "absent", NethValue = "present" });
                    continue;
                }
                if (!hasOurs && hasTheirs)
                {
                    result.Diffs.Add(new AccountFieldDiff { Address = addr, Field = "EXISTENCE", GethValue = "present", NethValue = "absent" });
                    continue;
                }

                if (ourAcct.Balance != theirAcct.Balance)
                    result.Diffs.Add(new AccountFieldDiff { Address = addr, Field = "BALANCE", GethValue = theirAcct.Balance.ToString(), NethValue = ourAcct.Balance.ToString() });
                if (ourAcct.Nonce != theirAcct.Nonce)
                    result.Diffs.Add(new AccountFieldDiff { Address = addr, Field = "NONCE", GethValue = theirAcct.Nonce.ToString(), NethValue = ourAcct.Nonce.ToString() });
                if (!ourAcct.Code.SequenceEqual(theirAcct.Code))
                    result.Diffs.Add(new AccountFieldDiff { Address = addr, Field = "CODE", GethValue = theirAcct.Code.ToHex(), NethValue = ourAcct.Code.ToHex() });

                var storageKeys = new HashSet<BigInteger>(ourAcct.Storage.Keys);
                storageKeys.UnionWith(theirAcct.Storage.Keys);
                foreach (var slot in storageKeys)
                {
                    ourAcct.Storage.TryGetValue(slot, out var oVal);
                    theirAcct.Storage.TryGetValue(slot, out var tVal);
                    var oTrim = TrimLeadingZeros(oVal ?? new byte[0]);
                    var tTrim = TrimLeadingZeros(tVal ?? new byte[0]);
                    if (!oTrim.SequenceEqual(tTrim))
                        result.Diffs.Add(new AccountFieldDiff
                        {
                            Address = addr,
                            Field = "STORAGE[" + slot + "]",
                            GethValue = tTrim.Length == 0 ? "0x" : "0x" + tTrim.ToHex(),
                            NethValue = oTrim.Length == 0 ? "0x" : "0x" + oTrim.ToHex()
                        });
                }
            }

            result.Signature = ChooseSignature(result);
            return result;
        }

        private PostStateSignature ChooseSignature(PostStateComparison c)
        {
            if (c.Diffs.Count == 0) return PostStateSignature.NO_DIFF;
            // EXISTENCE-only diffs
            var existenceDiffs = c.Diffs.Where(d => d.Field == "EXISTENCE").ToList();
            if (existenceDiffs.Count == c.Diffs.Count)
            {
                var extraInOurs = existenceDiffs.Count(d => d.NethValue == "present" && d.GethValue == "absent");
                var missingInOurs = existenceDiffs.Count(d => d.NethValue == "absent" && d.GethValue == "present");
                if (extraInOurs > 0 && missingInOurs == 0) return PostStateSignature.EXTRA_ACCOUNT_IN_OURS;
                if (missingInOurs > 0 && extraInOurs == 0) return PostStateSignature.MISSING_ACCOUNT_IN_OURS;
                return PostStateSignature.EXISTENCE_MIXED;
            }
            // Coinbase-only balance diff
            var coinbaseDiffs = c.Diffs.Where(d => d.Address == c.CoinbaseAddress).ToList();
            if (coinbaseDiffs.Count == c.Diffs.Count && coinbaseDiffs.All(d => d.Field == "BALANCE"))
                return PostStateSignature.COINBASE_BALANCE_DIFF;
            // Sender-only balance diff (refund cap suspect)
            var senderDiffs = c.Diffs.Where(d => d.Address == c.SenderAddress).ToList();
            if (senderDiffs.Count == c.Diffs.Count && senderDiffs.All(d => d.Field == "BALANCE"))
                return PostStateSignature.SENDER_BALANCE_DIFF;
            // Storage-only diffs
            if (c.Diffs.All(d => d.Field.StartsWith("STORAGE[", StringComparison.Ordinal)))
                return PostStateSignature.STORAGE_ONLY;
            // Balance-only across many accounts
            if (c.Diffs.All(d => d.Field == "BALANCE"))
                return PostStateSignature.MULTI_BALANCE_DIFF;
            return PostStateSignature.MIXED;
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null) return new byte[0];
            int i = 0;
            while (i < bytes.Length && bytes[i] == 0) i++;
            if (i == bytes.Length) return new byte[0];
            var res = new byte[bytes.Length - i];
            Buffer.BlockCopy(bytes, i, res, 0, res.Length);
            return res;
        }

        private sealed class AccountSnapshot
        {
            public BigInteger Balance;
            public BigInteger Nonce;
            public byte[] Code = new byte[0];
            public Dictionary<BigInteger, byte[]> Storage = new();
        }
    }

    public class PostStateComparison
    {
        public string CoinbaseAddress { get; set; }
        public string SenderAddress { get; set; }
        public List<AccountFieldDiff> Diffs { get; } = new();
        public PostStateSignature Signature { get; set; }

        public string Summary(int maxDiffs = 8)
        {
            var sb = new StringBuilder();
            sb.Append("Signature=").Append(Signature).Append(" (").Append(Diffs.Count).Append(" field diffs)");
            foreach (var d in Diffs.Take(maxDiffs))
            {
                sb.Append("\n    ").Append(d.Address).Append(" ").Append(d.Field)
                  .Append(" geth=").Append(Truncate(d.GethValue, 64)).Append(" neth=").Append(Truncate(d.NethValue, 64));
            }
            if (Diffs.Count > maxDiffs) sb.Append("\n    ... (+").Append(Diffs.Count - maxDiffs).Append(" more)");
            return sb.ToString();
        }
        private static string Truncate(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");
    }

    public class AccountFieldDiff
    {
        public string Address { get; set; }
        public string Field { get; set; }
        public string GethValue { get; set; }
        public string NethValue { get; set; }
    }

    public enum PostStateSignature
    {
        NO_DIFF,
        EXTRA_ACCOUNT_IN_OURS,        // We have account geth doesn't — missing touched-empty delete OR over-materialization
        MISSING_ACCOUNT_IN_OURS,      // Geth has account we don't — over-aggressive delete OR missing materialization
        EXISTENCE_MIXED,              // Both directions
        COINBASE_BALANCE_DIFF,        // coinbase tip/fee rule
        SENDER_BALANCE_DIFF,          // refund cap / gas accounting
        STORAGE_ONLY,                 // SSTORE refund / storage write semantics
        MULTI_BALANCE_DIFF,           // Multiple accounts have wrong balance — Transfer rule
        MIXED                         // Combination — needs deeper diagnosis
    }
}
