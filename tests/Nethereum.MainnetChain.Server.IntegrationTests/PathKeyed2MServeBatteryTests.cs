using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Documentation;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    [Collection("PathKeyed2MDb")]
    public class PathKeyed2MServeBatteryTests
    {
        private readonly ITestOutputHelper _out;
        public PathKeyed2MServeBatteryTests(ITestOutputHelper o) => _out = o;

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "Serve snap AccountRange, StorageRanges and TrieNodes verified against stored roots")]
        public async Task Serve_SnapPrimitives_VerifyAgainstStoredRoots_AtTip_InWindow_AndFloor()
        {
            var dir = Environment.GetEnvironmentVariable("NETHEREUM_PATHKEYED_2M_DB");
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            using var bundle = RocksDbChainStoreBundle.Open(
                dir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dir, PathKeyedState = true, TrieNodeHistoryBlocks = 1024, TrieNodeHistoryIndex = true,
                });

            var head = bundle.Metadata.GetLastBlock();
            var floor = bundle.NodeServing.Floor.FloorFor(head);
            _out.WriteLine($"committed-state head={head} floor={floor} window={head - floor}");
            Assert.True(head >= 50_000, $"path-keyed DB has too little committed state to validate serving; head={head}");

            var latestStore = ((ILatestProofServingBundle)bundle).LatestProofNodeStore;
            var selector = bundle.NodeServing as ISnapNodeStoreSelector;
            Assert.NotNull(latestStore);
            Assert.NotNull(selector);
            var handler = new PatriciaSnapRequestHandler(
                latestStore, new StateStoreBytecodeStore(bundle.State), selector: selector);

            var zero = new byte[32];
            var full = Filled(0xff);
            async Task<byte[]> RootAt(ulong n) => (await bundle.Blocks.GetByNumberAsync(n)).StateRoot;

            var headRoot = await RootAt(head);
            foreach (var n in new[] { head, head - 1, head - 512, floor + 1, floor })
            {
                var root = await RootAt(n);
                var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1, RootHash = root, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
                });
                Assert.NotEmpty(resp.Accounts);
                Assert.True(VerifyAccountRange(root, zero, resp), $"AccountRange as-of {n} must verify vs its stored root");
            }

            var belowFloor = await RootAt(floor - 1);
            var respLow = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 2, RootHash = belowFloor, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.Empty(respLow.Accounts);

            foreach (var n in new[] { head, floor + 1 })
            {
                var root = await RootAt(n);
                var tn = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 3, RootHash = root,
                    Paths = new List<List<byte[]>> { new List<byte[]> { Array.Empty<byte>() } },
                    ResponseBytes = 1024UL * 1024,
                });
                Assert.NotEmpty(tn.Nodes);
                Assert.True(new Sha3KeccackHashProvider().ComputeHash(tn.Nodes[0]).SequenceEqual(root),
                    $"served root node must hash to block {n} root");
            }

            var headResp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 4, RootHash = headRoot, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            byte[] contractHash = null, contractStorageRoot = null;
            foreach (var acc in headResp.Accounts)
            {
                var decoded = new AccountEncoder().Decode(SlimAccountEncoder.FromSlim(acc.Body));
                if (!ByteUtil.AreEqual(decoded.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                {
                    contractHash = acc.Hash; contractStorageRoot = decoded.StateRoot; break;
                }
            }
            Assert.NotNull(contractHash);
            var sr = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 5, RootHash = headRoot, AccountHashes = new List<byte[]> { contractHash },
                StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            var slotKeys = sr.Slots[0].Select(s => s.Hash).ToList();
            var slotVals = sr.Slots[0].Select(s => s.Data).ToList();
            var sProof = (IList<byte[]>)(sr.Proof ?? new List<byte[]>());
            Assert.True(ProofVerification.Current.Range.Verify(contractStorageRoot, zero, slotKeys, slotVals, sProof).Valid,
                "contract storage range must verify vs its storageHash");
            _out.WriteLine("served + verified: AccountRange (tip/in-window/floor) + below-floor decline, TrieNodes root, StorageRanges for a real contract");
        }

        private static bool VerifyAccountRange(byte[] stateRoot, byte[] origin, AccountRangeMessage resp)
        {
            var keys = new List<byte[]>(resp.Accounts.Count);
            var values = new List<byte[]>(resp.Accounts.Count);
            foreach (var e in resp.Accounts) { keys.Add(e.Hash); values.Add(SlimAccountEncoder.FromSlim(e.Body)); }
            var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return ProofVerification.Current.Range.Verify(stateRoot, origin, keys, values, proof).Valid;
        }

        private static byte[] Filled(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = b; return h; }
    }
}
