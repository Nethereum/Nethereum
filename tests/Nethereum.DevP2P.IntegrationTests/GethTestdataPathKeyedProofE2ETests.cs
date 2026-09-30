using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection("GethTestdataPathKeyedProof")]
    public class GethTestdataPathKeyedProofE2ETests : IClassFixture<GethPathKeyedReplayFixture>
    {
        private readonly GethPathKeyedReplayFixture _fx;
        private readonly ITestOutputHelper _output;

        public GethTestdataPathKeyedProofE2ETests(GethPathKeyedReplayFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _output = output;
        }

        [Fact]
        public void AllBlocks_PathKeyedComputedRoot_MatchesGethHeaderStateRoot()
        {
            _output.WriteLine($"Path-keyed replay: {_fx.Matched}/{_fx.BlockCount} blocks matched geth header stateRoot");
            if (_fx.FirstMismatch != null)
                _output.WriteLine($"First mismatch: {_fx.FirstMismatch}");
            Assert.Equal(_fx.BlockCount, _fx.Matched);
        }

        [Fact]
        public async Task GetProof_Latest_Account_VerifiesAgainstHeadRoot()
        {
            var head = _fx.Head;
            var headRoot = _fx.Roots[(int)head];
            var beacon = Eip4788Constants.BeaconRootsAddress;

            var resp = await _fx.GetProofAsync(beacon, new string[0], "latest");
            Assert.Null(resp.Error);
            var proof = GethPathKeyedReplayFixture.ToAccountProof(resp.Result);

            Assert.True(VerifyAccount(beacon, proof, headRoot), "LATEST account proof must verify vs the head root");
            Assert.False(VerifyAccount(beacon, proof, _fx.Roots[(int)head - 1]),
                "LATEST account proof must NOT verify vs a different (parent) root");
            _output.WriteLine($"LATEST head={head} beacon account proof verified against geth head root OK");
        }

        [Fact]
        public async Task GetProof_Latest_Storage_VerifiesAgainstStorageHash()
        {
            var head = _fx.Head;
            var beacon = Eip4788Constants.BeaconRootsAddress;
            var headTs = (BigInteger)(ulong)(await _fx.Node.GetBlockByNumberAsync(head)).Timestamp;
            var tsSlot = Eip4788Helpers.ComputeTimestampSlot(headTs, Eip4788Constants.HistoryBufferLength);

            var resp = await _fx.GetProofAsync(beacon, new[] { ToSlotHex(tsSlot) }, "latest");
            Assert.Null(resp.Error);
            var proof = GethPathKeyedReplayFixture.ToAccountProof(resp.Result);

            Assert.Single(proof.StorageProof);
            var sp = proof.StorageProof[0];
            Assert.Equal(headTs, sp.Value.Value);
            Assert.True(VerifyStorage(tsSlot, sp, proof.StorageHash.HexToByteArray()),
                "LATEST storage proof must verify against the account's storageHash");
        }

        [Theory]
        [InlineData(400)]
        [InlineData(450)]
        [InlineData(490)]
        public async Task GetProof_TipX_Historical_VerifiesAgainstBlockNRoot(int nInt)
        {
            ulong n = (ulong)nInt;
            var rootN = _fx.Roots[(int)n];
            var beacon = Eip4788Constants.BeaconRootsAddress;

            var rootBlob = _fx.Bundle.NodeServing.Journal.FindBlobAsOf(new byte[0], new byte[0], n);
            Assert.NotNull(rootBlob);
            var rootKeccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider().ComputeHash(rootBlob);
            Assert.True(rootKeccak.SequenceEqual(rootN),
                $"as-of-{n} account-trie root reverse-diff must hash to block {n} stateRoot (block-boundary, not an intra-block intermediate)");

            var resp = await _fx.GetProofAsync(beacon, new string[0], "0x" + n.ToString("x"));
            Assert.Null(resp.Error);
            var proof = GethPathKeyedReplayFixture.ToAccountProof(resp.Result);
            Assert.True(VerifyAccount(beacon, proof, rootN), $"as-of-{n} account proof must verify vs block {n} root");
            Assert.False(VerifyAccount(beacon, proof, _fx.Roots[(int)n - 1]),
                $"as-of-{n} account proof must NOT verify vs block {n - 1} root");
        }

        [Theory]
        [InlineData(490)]
        [InlineData(495)]
        public async Task Snap_GetAccountRange_ServesInWindowNonLatestRoot_ProofVerifies_AndDeclinesBelowFloor(int nInt)
        {
            ulong n = (ulong)nInt;
            Assert.True(n > _fx.Floor && n < _fx.Head, $"block {n} must be in-window: floor {_fx.Floor} < n < head {_fx.Head}");

            var latestStore = ((ILatestProofServingBundle)_fx.Bundle).LatestProofNodeStore;
            Assert.NotNull(latestStore);
            var selector = _fx.Bundle.NodeServing as ISnapNodeStoreSelector;
            Assert.NotNull(selector);
            var handler = new PatriciaSnapRequestHandler(
                latestStore, new StateStoreBytecodeStore(_fx.Bundle.State), selector: selector);

            var zero = new byte[32];
            var full = new byte[32];
            for (int i = 0; i < 32; i++) full[i] = 0xff;

            var rootN = _fx.Roots[(int)n];
            var respN = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = rootN,
                StartingHash = zero,
                LimitHash = full,
                ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.NotEmpty(respN.Accounts);
            Assert.True(VerifyAccountRange(rootN, zero, respN), $"as-of-{n} account range proof must verify vs block {n} root");

            var headRoot = _fx.Roots[(int)_fx.Head];
            var respHead = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 2,
                RootHash = headRoot,
                StartingHash = zero,
                LimitHash = full,
                ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.NotEmpty(respHead.Accounts);
            Assert.True(VerifyAccountRange(headRoot, zero, respHead), "head account range proof must verify vs head root");

            var belowFloorRoot = _fx.Roots[1];
            Assert.True(1 < _fx.Floor, $"block 1 must be below floor {_fx.Floor}");
            var respLow = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 3,
                RootHash = belowFloorRoot,
                StartingHash = zero,
                LimitHash = full,
                ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.Empty(respLow.Accounts);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(495)]
        public async Task Snap_GetStorageRanges_VerifiesAgainstStorageHash(int nInt)
        {
            ulong n = nInt == 0 ? _fx.Head : (ulong)nInt;
            var rootN = _fx.Roots[(int)n];
            var beacon = Eip4788Constants.BeaconRootsAddress;

            var pr = await _fx.GetProofAsync(beacon, new string[0], nInt == 0 ? "latest" : "0x" + n.ToString("x"));
            Assert.Null(pr.Error);
            var storageHash = GethPathKeyedReplayFixture.ToAccountProof(pr.Result).StorageHash.HexToByteArray();

            var handler = NewSnapHandler();
            var zero = new byte[32];
            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 1,
                RootHash = rootN,
                AccountHashes = new List<byte[]> { AccountHash(beacon) },
                StartingHash = zero,
                LimitHash = Full(),
                ResponseBytes = 4UL * 1024 * 1024,
            });

            var slotKeys = resp.Slots[0].Select(s => s.Hash).ToList();
            var slotValues = resp.Slots[0].Select(s => s.Data).ToList();
            var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            Assert.NotEmpty(resp.Slots[0]);
            Assert.True(ProofVerification.Current.Range.Verify(storageHash, zero, slotKeys, slotValues, proof).Valid,
                $"as-of-{n} storage range must verify vs the beacon account's storageHash");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(495)]
        public async Task Snap_GetTrieNodes_RootNode_HashesToStateRoot(int nInt)
        {
            ulong n = nInt == 0 ? _fx.Head : (ulong)nInt;
            var rootN = _fx.Roots[(int)n];

            var handler = NewSnapHandler();
            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 1,
                RootHash = rootN,
                Paths = new List<List<byte[]>> { new List<byte[]> { new byte[] { 0x00 } } },
                ResponseBytes = 1024UL * 1024,
            });

            Assert.NotEmpty(resp.Nodes);
            var served = resp.Nodes[0];
            Assert.NotNull(served);
            var keccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider().ComputeHash(served);
            Assert.True(keccak.SequenceEqual(rootN), $"served account-trie root node must hash to block {n} stateRoot");
        }

        [Fact]
        public async Task GetProof_AtRetentionFloorBoundary_ServesAtAndAboveFloor_DeclinesBelow()
        {
            var beacon = Eip4788Constants.BeaconRootsAddress;
            ulong floor = _fx.Floor;
            Assert.True(floor >= 2 && floor + 1 <= _fx.Head, $"need room around floor {floor} (head {_fx.Head})");

            foreach (var n in new[] { floor, floor + 1 })
            {
                var resp = await _fx.GetProofAsync(beacon, new string[0], "0x" + n.ToString("x"));
                Assert.Null(resp.Error);
                var proof = GethPathKeyedReplayFixture.ToAccountProof(resp.Result);
                Assert.True(VerifyAccount(beacon, proof, _fx.Roots[(int)n]),
                    $"as-of-{n} (at/above floor {floor}) must serve and verify vs block {n} root");
            }

            var below = await _fx.GetProofAsync(beacon, new string[0], "0x" + (floor - 1).ToString("x"));
            Assert.NotNull(below.Error);
            Assert.Equal(-32000, below.Error.Code);
        }

        private PatriciaSnapRequestHandler NewSnapHandler()
        {
            var latestStore = ((ILatestProofServingBundle)_fx.Bundle).LatestProofNodeStore;
            var selector = _fx.Bundle.NodeServing as ISnapNodeStoreSelector;
            return new PatriciaSnapRequestHandler(
                latestStore, new StateStoreBytecodeStore(_fx.Bundle.State), selector: selector);
        }

        private static byte[] AccountHash(string address)
            => new Sha3Keccack().CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private static byte[] Full()
        {
            var f = new byte[32];
            for (int i = 0; i < 32; i++) f[i] = 0xff;
            return f;
        }

        private static bool VerifyAccountRange(byte[] stateRoot, byte[] origin, AccountRangeMessage resp)
        {
            var keys = new List<byte[]>(resp.Accounts.Count);
            var canonicalValues = new List<byte[]>(resp.Accounts.Count);
            foreach (var entry in resp.Accounts)
            {
                keys.Add(entry.Hash);
                canonicalValues.Add(SlimAccountEncoder.FromSlim(entry.Body));
            }
            var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return ProofVerification.Current.Range.Verify(stateRoot, origin, keys, canonicalValues, proof).Valid;
        }

        [Fact]
        public async Task GetProof_BelowRetainedFloor_Returns32000()
        {
            ulong belowFloor = 1;
            Assert.True(belowFloor < _fx.Floor, $"block {belowFloor} must be below the floor {_fx.Floor}");

            var resp = await _fx.GetProofAsync(
                Eip4788Constants.BeaconRootsAddress, new string[0], "0x" + belowFloor.ToString("x"));
            Assert.NotNull(resp.Error);
            Assert.Equal(-32000, resp.Error.Code);
            _output.WriteLine($"below-floor N={belowFloor}: -32000 ({resp.Error.Message})");
        }

        [Fact]
        public async Task HistoryIndexOff_Node_DeclinesHistorical_With32000()
        {
            using var scratch = GethChainReplay.Replay(pathKeyedWindow: 128, historyIndex: false, upToBlock: 12);

            Assert.Null(scratch.Bundle.NodeServing);
            Assert.False(((IHistoricalProofCapable)scratch.Node).CanServeProofAsOf(5, head: scratch.Head));

            var resp = await scratch.GetProofAsync(
                Eip4788Constants.BeaconRootsAddress, new string[0], "0x5");
            Assert.NotNull(resp.Error);
            Assert.Equal(-32000, resp.Error.Code);
            _output.WriteLine($"index-off historical N=5: NodeServing=null, -32000 ({resp.Error.Message})");
        }

        private static string ToSlotHex(BigInteger slot) => "0x" + slot.ToString("x");

        private static bool VerifyAccount(string address, AccountProof proof, byte[] stateRoot)
        {
            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var account = new Account
            {
                Balance = proof.Balance.Value,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray()
            };
            return ProofVerification.Current.Account.Verify(stateRoot, proofBytes, address, account);
        }

        private static bool VerifyStorage(BigInteger slot, StorageProof sp, byte[] storageHash)
        {
            var proofNodes = sp.Proof.Select(p => p.HexToByteArray()).ToList();
            var valueBytes = sp.Value.Value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var slotBytes = slot.ToBytesForRLPEncoding();
            return ProofVerification.Current.Storage.Verify(storageHash, proofNodes, slotBytes, valueBytes);
        }
    }

    public sealed class GethPathKeyedReplayFixture : IDisposable
    {
        private readonly GethChainReplay.Replayed _replayed;

        public GethPathKeyedReplayFixture()
        {
            _replayed = GethChainReplay.Replay(pathKeyedWindow: 128, historyIndex: true, upToBlock: 0);
        }

        public RocksDbChainStoreBundle Bundle => _replayed.Bundle;
        public FollowerChainNode Node => _replayed.Node;
        public IReadOnlyList<byte[]> Roots => _replayed.Roots;
        public ulong Head => _replayed.Head;
        public ulong Floor => _replayed.Floor;
        public int BlockCount => _replayed.BlockCount;
        public int Matched => _replayed.Matched;
        public string FirstMismatch => _replayed.FirstMismatch;

        public Task<RpcResponseMessage> GetProofAsync(string address, string[] storageKeys, string blockParam)
            => _replayed.GetProofAsync(address, storageKeys, blockParam);

        public static AccountProof ToAccountProof(object result) => GethChainReplay.ToAccountProof(result);

        public void Dispose() => _replayed.Dispose();
    }
}
