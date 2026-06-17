using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.SpecTests.Eth69;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model.P2P.Snap;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Nethereum.DevP2P.SpecTests.Snap
{
    /// <summary>
    /// snap/1 protocol-coverage tests against a live mainnet peer. Each test
    /// negotiates eth/* + snap/1, looks up the peer's head state root via
    /// JSON-RPC, then exercises one specific request/response pair documented
    /// in devp2p/caps/snap.md. Tests skip when the peer does not advertise
    /// snap/1 — typical for archive nodes that don't serve fast-sync clients.
    /// </summary>
    public class MainnetPeerSnapTests : MainnetPeerTestBase
    {
        private const ulong ResponseBudgetBytes = 2 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        // USDC (Circle) mainnet proxy — long-standing contract with substantial
        // populated storage and known runtime bytecode prefix (`0x6080`). Picked
        // for the storage + code probes because its presence is independent of
        // any recent state-trie reshuffle and is shared across forks.
        private const string UsdcAddressHex = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48";

        public MainnetPeerSnapTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public Task GetAccountRange_AtKnownBlock_ReturnsPagedAccounts()
            => RunWithSnapSessionAsync(async (session, ctx, ct) =>
            {
                var stateRoot = await ResolveHeadStateRootAsync(ctx);
                var startingHash = new byte[32];
                var limitHash = FilledHash(0xff);

                Output.WriteLine($"  GetAccountRange stateRoot=0x{stateRoot.ToHex()} budget={ResponseBudgetBytes}");
                using var reqCts = LinkedTimeout(ct);
                var resp = await session.GetAccountRangeAsync(stateRoot, startingHash, limitHash, ResponseBudgetBytes, reqCts.Token);

                Output.WriteLine($"  AccountRange: accounts={resp.Accounts.Count} proofNodes={resp.Proof.Count}");
                Assert.NotEmpty(resp.Accounts);
                Assert.NotEmpty(resp.Proof);
                Assert.All(resp.Accounts, a =>
                {
                    Assert.NotNull(a.Hash);
                    Assert.Equal(32, a.Hash.Length);
                    Assert.NotNull(a.Body);
                    Assert.NotEmpty(a.Body);
                });

                for (int i = 1; i < resp.Accounts.Count; i++)
                {
                    var prev = resp.Accounts[i - 1].Hash;
                    var curr = resp.Accounts[i].Hash;
                    Assert.True(CompareHashes(prev, curr) < 0,
                        $"accounts[{i - 1}].hash 0x{prev.ToHex()} must be < accounts[{i}].hash 0x{curr.ToHex()}");
                }
            });

        [SkippableFact]
        public Task GetStorageRanges_KnownContract_ReturnsSlots()
            => RunWithSnapSessionAsync(async (session, ctx, ct) =>
            {
                var stateRoot = await ResolveHeadStateRootAsync(ctx);
                var accountHash = AccountHash(UsdcAddressHex);
                var startingHash = new byte[32];
                var limitHash = FilledHash(0xff);

                Output.WriteLine($"  GetStorageRanges account=0x{accountHash.ToHex()} stateRoot=0x{stateRoot.ToHex()}");
                using var reqCts = LinkedTimeout(ct);
                var resp = await session.GetStorageRangesAsync(
                    stateRoot,
                    new List<byte[]> { accountHash },
                    startingHash, limitHash, ResponseBudgetBytes, reqCts.Token);

                Output.WriteLine($"  StorageRanges: accountGroups={resp.Slots.Count} proofNodes={resp.Proof.Count}");
                Skip.If(resp.Slots.Count == 0,
                    "Peer returned empty StorageRanges — likely doesn't hold storage for this account at the requested root");

                var slots = resp.Slots[0];
                Output.WriteLine($"  slots[account 0]: {slots.Count}");
                Assert.NotEmpty(slots);
                Assert.All(slots, s =>
                {
                    Assert.NotNull(s.Hash);
                    Assert.Equal(32, s.Hash.Length);
                    Assert.NotNull(s.Data);
                    Assert.NotEmpty(s.Data);
                });

                for (int i = 1; i < slots.Count; i++)
                {
                    Assert.True(CompareHashes(slots[i - 1].Hash, slots[i].Hash) < 0,
                        $"storage slot hashes must be in increasing order at index {i}");
                }
            });

        [SkippableFact]
        public Task GetByteCodes_KnownContract_ReturnsBytecode()
            => RunWithSnapSessionAsync(async (session, ctx, ct) =>
            {
                var codeHex = await ctx.Web3.Eth.GetCode.SendRequestAsync(UsdcAddressHex);
                Skip.If(string.IsNullOrEmpty(codeHex) || codeHex == "0x",
                    $"Peer returned empty code for {UsdcAddressHex} — not a contract or peer state pruned");

                var expectedCode = codeHex.HexToByteArray();
                var codeHash = new Sha3Keccack().CalculateHash(expectedCode);

                Output.WriteLine($"  GetByteCodes codeHash=0x{codeHash.ToHex()} expectedLen={expectedCode.Length}");
                using var reqCts = LinkedTimeout(ct);
                var resp = await session.GetByteCodesAsync(
                    new List<byte[]> { codeHash }, ResponseBudgetBytes, reqCts.Token);

                Output.WriteLine($"  ByteCodes: codes={resp.Codes.Count}");
                Assert.Single(resp.Codes);
                var returned = resp.Codes[0];
                Assert.NotNull(returned);
                Assert.NotEmpty(returned);
                Assert.Equal(expectedCode.Length, returned.Length);
                Assert.Equal(0x60, returned[0]);
                Assert.Equal(0x80, returned[1]);

                var returnedHash = new Sha3Keccack().CalculateHash(returned);
                Assert.Equal(codeHash.ToHex(), returnedHash.ToHex());
            });

        [SkippableFact]
        public Task GetTrieNodes_StateRoot_ReturnsValidNode()
            => RunWithSnapSessionAsync(async (session, ctx, ct) =>
            {
                var stateRoot = await ResolveHeadStateRootAsync(ctx);

                Output.WriteLine($"  GetTrieNodes stateRoot=0x{stateRoot.ToHex()} path=[[]]");
                using var reqCts = LinkedTimeout(ct);
                var resp = await session.GetTrieNodesAsync(
                    stateRoot,
                    new List<List<byte[]>> { new List<byte[]> { new byte[0] } },
                    ResponseBudgetBytes, reqCts.Token);

                Output.WriteLine($"  TrieNodes: nodes={resp.Nodes.Count}");
                Assert.Single(resp.Nodes);
                var node = resp.Nodes[0];
                Assert.NotNull(node);
                Assert.NotEmpty(node);

                var nodeHash = new Sha3Keccack().CalculateHash(node);
                Output.WriteLine($"  node[0]: len={node.Length} hash=0x{nodeHash.ToHex()}");
                Assert.Equal(stateRoot.ToHex(), nodeHash.ToHex());
                Assert.True(node[0] >= 0xc0,
                    $"trie node must start with an RLP list prefix (>= 0xc0); got 0x{node[0]:x2}");
            });

        private Task RunWithSnapSessionAsync(Func<MainnetPeerSession, PeerContext, CancellationToken, Task> body)
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                if (!session.SupportsSnap)
                {
                    foreach (var cap in session.Connection.SharedCapabilities)
                        Output.WriteLine($"  cap: {cap.Name}/{cap.Version}");
                    Skip.If(true,
                        "Peer does not advertise snap/1 — skipping snap-protocol tests. " +
                        "Use a snap-serving peer (e.g. geth in snap-sync mode; see docs/peer-harness).");
                }
                Output.WriteLine($"  snap/1 negotiated at offset 0x{session.Connection.GetCapabilityOffset("snap"):x2}");
                await body(session, ctx, ct);
            });

        private static async Task<byte[]> ResolveHeadStateRootAsync(PeerContext ctx)
        {
            var head = await ctx.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            if (head?.StateRoot == null)
                throw new InvalidOperationException("Peer's head block has no stateRoot — JSON-RPC response is malformed");
            return head.StateRoot.HexToByteArray();
        }

        private static byte[] AccountHash(string addressHex)
        {
            var address = addressHex.HexToByteArray();
            if (address.Length != 20)
                throw new ArgumentException($"address must be 20 bytes, got {address.Length}", nameof(addressHex));
            return new Sha3Keccack().CalculateHash(address);
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static int CompareHashes(byte[] a, byte[] b)
        {
            var len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            }
            return a.Length.CompareTo(b.Length);
        }

        private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(RequestTimeout);
            return cts;
        }
    }
}
