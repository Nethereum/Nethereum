using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Emits a <c>MainnetBlockFixture</c>-shaped JSON fixture for a single
    /// mainnet block on demand. Uses existing primitives — no new tracer:
    ///
    ///   - Sender addresses recovered from tx signatures
    ///     (ISignedTransaction.GetSenderAddress()).
    ///   - Tx.To and header.Coinbase taken from the block data.
    ///   - Dirty addresses from <see cref="IStateStore.GetDirtyAccountAddressesAsync"/>
    ///     to catch sub-CALL touches the tx didn't address directly
    ///     (precompiles, contract callees, etc.).
    ///
    /// Pre-state is captured BEFORE the block runs by reading the current
    /// store values for sender + to + coinbase. Post-state assertions are
    /// captured AFTER for the union (sender + to + coinbase + dirty).
    ///
    /// Output shape matches <c>tests/Nethereum.EVM.UnitTests/GeneralStateTests/MainnetBlockFixture.cs</c>
    /// so the fixture drops straight into <c>Fixtures/MainnetBlocks/</c>
    /// for the MainnetBlockReplayTests theory to consume.
    ///
    /// Use cases:
    ///   - <c>--dump-fixture-blocks N,N,N</c>: emit fixture as sync passes
    ///     each listed block. Used to seed regression cells for known
    ///     historic divergences.
    ///   - <c>--dump-fixture-on-mismatch DIR</c>: emit fixture for every
    ///     state-root mismatch as sync hits new bugs. Closes the
    ///     mismatch -&gt; fixture -&gt; test -&gt; fix loop.
    /// </summary>
    internal static class MainnetBlockFixtureEmitter
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public sealed class PreCapture
        {
            public Dictionary<string, AccountSnapshot> Accounts { get; set; } = new();
        }

        public sealed class AccountSnapshot
        {
            public string Balance { get; set; } = "0x0";
            public string Nonce { get; set; } = "0x0";
            public string Code { get; set; }
            public Dictionary<string, string> Storage { get; set; } = new();
        }

        /// <summary>
        /// Read pre-state for the addresses we know will be touched
        /// (sender + to + coinbase) BEFORE the block runs. The caller passes
        /// the address set; we just snapshot the store's current values.
        /// </summary>
        public static async Task<PreCapture> CaptureKnownAddressesAsync(
            IStateStore state, IEnumerable<string> addresses)
        {
            var capture = new PreCapture();
            foreach (var rawAddr in addresses)
            {
                if (string.IsNullOrEmpty(rawAddr)) continue;
                var addr = rawAddr.ToLowerInvariant();
                if (capture.Accounts.ContainsKey(addr)) continue;
                capture.Accounts[addr] = await SnapshotAccountAsync(state, addr);
            }
            return capture;
        }

        public static IEnumerable<string> CollectKnownAddresses(BlockHeader header, IList<ISignedTransaction> txs)
        {
            yield return header.Coinbase;
            // Always pre-capture the 9 standard precompiles so fixtures
            // include their pre-state (typically: absent at early blocks,
            // empty-leaf after first touch). Critical for blocks like
            // 49,439 (IDENTITY 0x04) and 505,137 (SHA256 0x02) where
            // precompile touched-empty materialisation is the divergence.
            for (int i = 1; i <= 9; i++)
            {
                yield return "0x" + new string('0', 39) + i.ToString("x");
            }
            foreach (var tx in txs ?? new List<ISignedTransaction>())
            {
                string sender = null;
                try { sender = tx.GetSenderAddress(); } catch { /* malformed tx — skip */ }
                if (!string.IsNullOrEmpty(sender)) yield return sender;
                string receiver = null;
                try { receiver = tx.GetReceiverAddress(); } catch { /* contract creation or malformed */ }
                if (!string.IsNullOrEmpty(receiver)) yield return receiver;
            }
        }

        public static async Task EmitFromRecorderAsync(
            string outputDir,
            BlockHeader header,
            IList<ISignedTransaction> txs,
            IList<BlockHeader> uncles,
            IStateStore state,
            FixturePreStateRecorder recorder,
            string scenario,
            string errorOrNull)
        {
            Directory.CreateDirectory(outputDir);
            long blockNumber = (long)header.BlockNumber;
            string path = Path.Combine(outputDir, $"block-{blockNumber}.json");
            var preStateAddrs = new HashSet<string>(recorder.FirstReadAccount.Keys);
            foreach (string item in CollectKnownAddresses(header, txs))
            {
                if (!string.IsNullOrEmpty(item)) preStateAddrs.Add(item.ToLowerInvariant());
            }
            var codeByHash = new Dictionary<string, byte[]>();
            foreach (var kvp in recorder.FirstReadCode)
            {
                if (kvp.Key != null && kvp.Value != null && kvp.Value.Length != 0)
                    codeByHash[kvp.Key.ToHex()] = kvp.Value;
            }
            var preState = new Dictionary<string, object>();
            foreach (string addr in preStateAddrs)
            {
                Account account = (!recorder.FirstReadAccount.TryGetValue(addr, out var value)) ? (await state.GetAccountAsync(addr)) : value;
                var entry = new Dictionary<string, object>
                {
                    ["balance"] = (account != null) ? ("0x" + new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x")) : "0x0",
                    ["nonce"] = (account != null) ? ("0x" + new BigInteger(account.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x")) : "0x0",
                    ["code"] = "0x"
                };
                if (account?.CodeHash != null && codeByHash.TryGetValue(account.CodeHash.ToHex(), out var codeBytes))
                {
                    entry["code"] = "0x" + codeBytes.ToHex();
                }
                var storageDict = new Dictionary<string, string>();
                foreach (var st in recorder.FirstReadStorage)
                {
                    if (st.Key.addr == addr && st.Value != null && st.Value.Length != 0)
                    {
                        storageDict["0x" + st.Key.slot.ToString("x")] = "0x" + st.Value.ToHex();
                    }
                }
                entry["storage"] = storageDict;
                preState[addr] = entry;
            }
            var dirtyAddrs = (await state.GetDirtyAccountAddressesAsync()).Select(a => a.ToLowerInvariant()).ToHashSet();
            foreach (var item4 in preStateAddrs) dirtyAddrs.Add(item4);
            var postAssertions = new Dictionary<string, object>();
            foreach (string addr in dirtyAddrs)
            {
                Account account = await state.GetAccountAsync(addr);
                var entry = new Dictionary<string, object>();
                if (account == null) entry["exists"] = false;
                else
                {
                    entry["balance"] = "0x" + new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
                    entry["nonce"] = "0x" + new BigInteger(account.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
                    entry["codeHash"] = "0x" + (account.CodeHash ?? Array.Empty<byte>()).ToHex();
                }
                postAssertions[addr] = entry;
            }
            var fixture = new Dictionary<string, object>
            {
                ["blockNumber"] = blockNumber,
                ["scenario"] = scenario ?? "",
                ["header"] = BuildHeaderFixture(header),
                ["transactionsRlp"] = (txs ?? new List<ISignedTransaction>()).Select(t => "0x" + t.GetRLPEncoded().ToHex()).ToList(),
                ["uncles"] = (uncles ?? new List<BlockHeader>()).Select(BuildHeaderFixture).ToList(),
                ["preState"] = preState,
                ["postAssertions"] = postAssertions,
                ["captureError"] = errorOrNull
            };
            File.WriteAllText(path, JsonSerializer.Serialize(fixture, JsonOpts));
        }

        public static async Task EmitAsync(
            string outputDir,
            BlockHeader header,
            IList<ISignedTransaction> txs,
            IList<BlockHeader> uncles,
            IStateStore state,
            PreCapture preCapture,
            string scenario,
            string errorOrNull)
        {
            Directory.CreateDirectory(outputDir);
            var blockNumber = (long)header.BlockNumber;
            var path = Path.Combine(outputDir, $"block-{blockNumber}.json");

            // Post-state touched set = preCapture addresses (sender/to/coinbase)
            // ∪ dirty addresses (sub-CALL touches: precompiles, callees, etc.).
            var dirty = (await state.GetDirtyAccountAddressesAsync())
                .Select(a => a.ToLowerInvariant())
                .ToHashSet();
            foreach (var a in preCapture.Accounts.Keys) dirty.Add(a);

            var postAssertions = new Dictionary<string, object>();
            foreach (var addr in dirty)
            {
                var account = await state.GetAccountAsync(addr);
                var assertion = new Dictionary<string, object>();
                if (account == null)
                {
                    assertion["exists"] = false;
                }
                else
                {
                    assertion["balance"] = "0x" + new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
                    assertion["nonce"] = "0x" + new BigInteger(account.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
                    assertion["codeHash"] = "0x" + (account.CodeHash ?? Array.Empty<byte>()).ToHex();
                }
                postAssertions[addr] = assertion;
            }

            var fixture = new Dictionary<string, object>
            {
                ["blockNumber"] = blockNumber,
                ["scenario"] = scenario ?? "",
                ["header"] = BuildHeaderFixture(header),
                ["transactionsRlp"] = (txs ?? new List<ISignedTransaction>())
                    .Select(t => "0x" + t.GetRLPEncoded().ToHex()).ToList(),
                ["uncles"] = (uncles ?? new List<BlockHeader>())
                    .Select(BuildHeaderFixture).ToList(),
                ["preState"] = preCapture.Accounts.ToDictionary(
                    kv => kv.Key,
                    kv => (object)new Dictionary<string, object>
                    {
                        ["balance"] = kv.Value.Balance,
                        ["nonce"] = kv.Value.Nonce,
                        ["code"] = kv.Value.Code ?? "0x",
                        ["storage"] = kv.Value.Storage
                    }),
                ["postAssertions"] = postAssertions,
                ["captureError"] = errorOrNull
            };

            File.WriteAllText(path, JsonSerializer.Serialize(fixture, JsonOpts));
        }

        private static async Task<AccountSnapshot> SnapshotAccountAsync(IStateStore state, string address)
        {
            var account = await state.GetAccountAsync(address);
            var snap = new AccountSnapshot();
            if (account == null)
            {
                return snap;
            }
            snap.Balance = "0x" + new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
            snap.Nonce = "0x" + new BigInteger(account.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x");
            if (account.CodeHash != null && account.CodeHash.Length == 32)
            {
                var code = await state.GetCodeAsync(account.CodeHash);
                if (code != null && code.Length > 0)
                {
                    snap.Code = "0x" + code.ToHex();
                }
            }
            return snap;
        }

        private static Dictionary<string, object> BuildHeaderFixture(BlockHeader h)
        {
            return new Dictionary<string, object>
            {
                ["parentHash"] = "0x" + (h.ParentHash ?? Array.Empty<byte>()).ToHex(),
                ["unclesHash"] = "0x" + (h.UnclesHash ?? Array.Empty<byte>()).ToHex(),
                ["coinbase"] = h.Coinbase?.ToLowerInvariant(),
                ["stateRoot"] = "0x" + (h.StateRoot ?? Array.Empty<byte>()).ToHex(),
                ["transactionsRoot"] = "0x" + (h.TransactionsHash ?? Array.Empty<byte>()).ToHex(),
                ["receiptsRoot"] = "0x" + (h.ReceiptHash ?? Array.Empty<byte>()).ToHex(),
                ["logsBloom"] = "0x" + (h.LogsBloom ?? Array.Empty<byte>()).ToHex(),
                ["difficulty"] = "0x" + new BigInteger(h.Difficulty.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x"),
                ["number"] = "0x" + new BigInteger(h.BlockNumber.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x"),
                ["gasLimit"] = "0x" + h.GasLimit.ToString("x"),
                ["gasUsed"] = "0x" + h.GasUsed.ToString("x"),
                ["timestamp"] = "0x" + h.Timestamp.ToString("x"),
                ["extraData"] = "0x" + (h.ExtraData ?? Array.Empty<byte>()).ToHex(),
                ["mixHash"] = "0x" + (h.MixHash ?? Array.Empty<byte>()).ToHex(),
                ["nonce"] = "0x" + (h.Nonce ?? Array.Empty<byte>()).ToHex(),
                ["baseFee"] = h.BaseFee.HasValue
                    ? ("0x" + new BigInteger(h.BaseFee.Value.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x"))
                    : null,
                ["withdrawalsRoot"] = h.WithdrawalsRoot != null ? ("0x" + h.WithdrawalsRoot.ToHex()) : null,
                ["parentBeaconBlockRoot"] = h.ParentBeaconBlockRoot != null ? ("0x" + h.ParentBeaconBlockRoot.ToHex()) : null
            };
        }
    }
}
