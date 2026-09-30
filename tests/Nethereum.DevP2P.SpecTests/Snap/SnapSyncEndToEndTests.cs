using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    public class SnapSyncEndToEndTests
    {
        private class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public void Put(byte[] hash, byte[] code) { _codes[hash] = code; }
            public byte[] Get(byte[] hash) => _codes.TryGetValue(hash, out var v) ? v : null;
        }

        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, InMemoryBytecodeStore codes, byte[] rootHash, int accountCount)
            BuildSourceState(int accountCount = 24, int withStorage = 6, int withCode = 6)
        {
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var codes = new InMemoryBytecodeStore();

            for (int i = 0; i < accountCount; i++)
            {
                var addrHash = keccak.CalculateHash(new[] { (byte)(i & 0xff), (byte)0xCC });

                byte[] storageRoot = DefaultValues.EMPTY_TRIE_HASH;
                if (i < withStorage)
                {
                    var st = new PatriciaTrie(storage);
                    for (int s = 0; s < 8; s++)
                    {
                        var slotHash = keccak.CalculateHash(new[] { (byte)i, (byte)s, (byte)0xDD });
                        var slotValue = new byte[] { (byte)(0x90 | (s & 0x7f)), (byte)(s + i) };
                        st.Put(slotHash, slotValue);
                    }
                    st.SaveDirtyNodesToStorage();
                    storageRoot = st.Root.GetHash();
                }

                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (i < withCode)
                {
                    var code = new byte[] { 0x60, (byte)(0x10 + i), 0xF3 };
                    codeHash = hashProvider.ComputeHash(code);
                    codes.Put(codeHash, code);
                }

                var body = new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)((ulong)(i + 1) * 100UL),
                    StateRoot = storageRoot,
                    CodeHash = codeHash
                });
                trie.Put(addrHash, body);
            }
            trie.SaveDirtyNodesToStorage();
            return (trie, storage, codes, trie.Root.GetHash(), accountCount);
        }

        [Fact]
        public async Task SyncStateAsync_AgainstHonestPeer_ProducesMatchingStateRoot()
        {
            var (sourceTrie, sourceStorage, sourceCodes, sourceRoot, sourceCount) = BuildSourceState();

            var handler = new PatriciaSnapRequestHandler(sourceStorage, sourceCodes);
            var peer = new InProcessSnapPeer(handler);
            var client = new SnapSyncClient(peer, accountsPerRequest: 8, responseBytesBudget: 1_000_000UL);

            var result = await client.SyncStateAsync(sourceRoot);

            Assert.True(result.RootMatchesTarget,
                $"Computed root {result.ComputedRoot.ToHex()} did not match target {sourceRoot.ToHex()}");
            Assert.Equal(sourceCount, result.AccountCount);
        }

        [Fact]
        public async Task SyncStateAsync_PullsBytecodesForContractAccounts()
        {
            var (_, sourceStorage, sourceCodes, sourceRoot, _) =
                BuildSourceState(accountCount: 8, withStorage: 0, withCode: 8);

            var handler = new PatriciaSnapRequestHandler(sourceStorage, sourceCodes);
            var peer = new InProcessSnapPeer(handler);
            var client = new SnapSyncClient(peer);

            var result = await client.SyncStateAsync(sourceRoot);

            Assert.Equal(8, result.BytecodeByHash.Count);
            foreach (var (codeHashHex, code) in result.BytecodeByHash)
            {
                var hashProvider = new Sha3KeccackHashProvider();
                var recomputed = hashProvider.ComputeHash(code);
                Assert.Equal(codeHashHex, recomputed.ToHex());
            }
        }

        [Fact]
        public async Task SyncStateAsync_PullsStorageTriesForAccountsWithStorage()
        {
            var (_, sourceStorage, sourceCodes, sourceRoot, _) =
                BuildSourceState(accountCount: 4, withStorage: 4, withCode: 0);

            var handler = new PatriciaSnapRequestHandler(sourceStorage, sourceCodes);
            var peer = new InProcessSnapPeer(handler);
            var client = new SnapSyncClient(peer);

            var result = await client.SyncStateAsync(sourceRoot);
            Assert.True(result.RootMatchesTarget);
        }

        [Fact]
        public async Task SyncStateAsync_PeerReturnsTamperedAccountBody_RejectedOnBoundaryProof()
        {
            var (_, sourceStorage, sourceCodes, sourceRoot, _) = BuildSourceState(accountCount: 16);
            var honestHandler = new PatriciaSnapRequestHandler(sourceStorage, sourceCodes);

            var dishonestPeer = new TamperingSnapPeer(honestHandler);
            var client = new SnapSyncClient(dishonestPeer);

            await Assert.ThrowsAnyAsync<System.Exception>(
                async () => await client.SyncStateAsync(sourceRoot));
        }

        private class TamperingSnapPeer : ISnapPeer
        {
            private readonly ISnapRequestHandler _inner;
            public TamperingSnapPeer(ISnapRequestHandler inner) { _inner = inner; }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, System.Threading.CancellationToken ct = default)
            {
                var resp = await _inner.GetAccountRangeAsync(r, ct);
                if (resp.Accounts.Count > 0)
                {
                    resp.Accounts[0].Body[0] ^= 0xff;
                }
                return resp;
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, System.Threading.CancellationToken ct = default) => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, System.Threading.CancellationToken ct = default) => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, System.Threading.CancellationToken ct = default) => _inner.GetTrieNodesAsync(r, ct);
        }
    }
}
