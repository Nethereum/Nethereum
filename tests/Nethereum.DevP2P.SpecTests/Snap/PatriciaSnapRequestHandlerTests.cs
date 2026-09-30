using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
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
    public class PatriciaSnapRequestHandlerTests
    {
        private class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public void Put(byte[] codeHash, byte[] code) { _codes[codeHash] = code; }
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash, out var v) ? v : null;
        }

        private class Fixture
        {
            public PatriciaTrie StateTrie;
            public InMemoryContentNodeStore TrieStorage;
            public InMemoryBytecodeStore Bytecodes;
            public List<(byte[] hash, byte[] body, byte[] storageRoot)> Accounts = new();
            public Dictionary<string, byte[]> StorageRootByAccount = new();
            public Dictionary<string, List<(byte[] slotHash, byte[] slotValue)>> StorageByAccount = new();
        }

        private static Fixture BuildFixture(int accountCount = 32, int withStorage = 8, int withCode = 8)
        {
            var f = new Fixture();
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            f.TrieStorage = new InMemoryContentNodeStore();
            f.Bytecodes = new InMemoryBytecodeStore();
            f.StateTrie = new PatriciaTrie(f.TrieStorage);

            for (int i = 0; i < accountCount; i++)
            {
                var addrSeed = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff), (byte)0xAA });

                byte[] storageRoot = DefaultValues.EMPTY_TRIE_HASH;
                if (i < withStorage)
                {
                    var storageTrie = new PatriciaTrie(f.TrieStorage);
                    var slots = new List<(byte[], byte[])>();
                    for (int s = 0; s < 16; s++)
                    {
                        var slotHash = keccak.CalculateHash(new[] { (byte)i, (byte)s, (byte)0xBB });
                        var slotValue = new byte[] { (byte)(0x80 | (s & 0x7f)), (byte)(s ^ 0x42) };
                        storageTrie.Put(slotHash, slotValue);
                        slots.Add((slotHash, slotValue));
                    }
                    storageTrie.SaveDirtyNodesToStorage();
                    storageRoot = storageTrie.Root.GetHash();
                    slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Item1, b.Item1));
                    f.StorageByAccount[addrSeed.ToHex()] = slots;
                    f.StorageRootByAccount[addrSeed.ToHex()] = storageRoot;
                }

                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (i < withCode)
                {
                    var code = new byte[] { (byte)0x60, (byte)(i & 0xff), (byte)0xFF };
                    codeHash = hashProvider.ComputeHash(code);
                    f.Bytecodes.Put(codeHash, code);
                }

                var account = new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)((ulong)(i + 1) * 1_000UL),
                    StateRoot = storageRoot,
                    CodeHash = codeHash
                };
                var body = new AccountEncoder().Encode(account);
                f.StateTrie.Put(addrSeed, body);
                f.Accounts.Add((addrSeed, body, storageRoot));
            }
            f.StateTrie.SaveDirtyNodesToStorage();
            f.Accounts.Sort((a, b) => ByteArrayComparer.Current.Compare(a.hash, b.hash));
            return f;
        }

        [Fact]
        public async Task GetAccountRange_FullRange_ReturnsAllAccountsInOrder()
        {
            var f = BuildFixture();
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = f.StateTrie.Root.GetHash(),
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(f.Accounts.Count, resp.Accounts.Count);
            for (int i = 0; i < f.Accounts.Count; i++)
            {
                Assert.Equal(f.Accounts[i].hash.ToHex(), resp.Accounts[i].Hash.ToHex());
                var expectedSlim = SlimAccountEncoder.ToSlim(f.Accounts[i].body);
                Assert.Equal(expectedSlim.ToHex(), resp.Accounts[i].Body.ToHex());
            }
            Assert.NotEmpty(resp.Proof);
        }

        [Fact]
        public async Task GetAccountRange_MiddleRange_ReturnsCorrectSliceWithVerifiableBoundaries()
        {
            var f = BuildFixture(32);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var rootHash = f.StateTrie.Root.GetHash();

            var startKey = f.Accounts[10].hash;
            var limit = f.Accounts[20].hash;

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 7,
                RootHash = rootHash,
                StartingHash = startKey,
                LimitHash = limit,
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(11, resp.Accounts.Count);
            Assert.Equal(startKey.ToHex(), resp.Accounts.First().Hash.ToHex());
            Assert.Equal(limit.ToHex(), resp.Accounts.Last().Hash.ToHex());

            var hashProvider = new Sha3KeccackHashProvider();
            var proofStorage = new InMemoryContentNodeStore();
            foreach (var node in resp.Proof) proofStorage.Put(hashProvider.ComputeHash(node), node);
            var verifyTrie = new PatriciaTrie(rootHash, proofStorage);
            Assert.Equal(SlimAccountEncoder.ToSlim(verifyTrie.Get(startKey)).ToHex(),
                         resp.Accounts.First().Body.ToHex());
            Assert.Equal(SlimAccountEncoder.ToSlim(new PatriciaTrie(rootHash, proofStorage).Get(limit)).ToHex(),
                         resp.Accounts.Last().Body.ToHex());
        }

        [Fact]
        public async Task GetAccountRange_ByteBudget_StopsAndReturnsProof()
        {
            var f = BuildFixture(64);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 9,
                RootHash = f.StateTrie.Root.GetHash(),
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 500UL
            });

            Assert.InRange(resp.Accounts.Count, 1, f.Accounts.Count - 1);
            Assert.NotEmpty(resp.Proof);
        }

        [Fact]
        public async Task GetByteCodes_ReturnsCodesByHash()
        {
            var f = BuildFixture(8, withStorage: 0, withCode: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var codeHashes = new List<byte[]>();
            var expectedCodes = new List<byte[]>();
            foreach (var (_, body, _) in f.Accounts)
            {
                var acc = new AccountEncoder().Decode(body);
                if (!ByteUtil.AreEqual(acc.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                {
                    codeHashes.Add(acc.CodeHash);
                    expectedCodes.Add(f.Bytecodes.Get(acc.CodeHash));
                }
            }
            Assert.NotEmpty(codeHashes);

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 11,
                Hashes = codeHashes,
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(expectedCodes.Count, resp.Codes.Count);
            for (int i = 0; i < expectedCodes.Count; i++)
                Assert.Equal(expectedCodes[i].ToHex(), resp.Codes[i].ToHex());
        }

        [Fact]
        public async Task GetByteCodes_UnknownHash_IsOmittedFromResponse()
        {
            var f = BuildFixture(4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 13,
                Hashes = new List<byte[]> { FilledHash(0x44) },
                ResponseBytes = 1_000_000UL
            });

            Assert.Empty(resp.Codes);
        }

        [Fact]
        public async Task GetByteCodes_EmptyDataHash_ReturnsEmptyCode()
        {
            var f = BuildFixture(4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 21,
                Hashes = new List<byte[]>
                {
                    Nethereum.Model.DefaultValues.EMPTY_DATA_HASH,
                    Nethereum.Model.DefaultValues.EMPTY_DATA_HASH,
                    Nethereum.Model.DefaultValues.EMPTY_DATA_HASH
                },
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(3, resp.Codes.Count);
            foreach (var c in resp.Codes) Assert.Empty(c);
        }

        [Fact]
        public async Task GetStorageRanges_ReturnsSlotsForRequestedAccounts()
        {
            var f = BuildFixture(8, withStorage: 4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var accountsWithStorage = f.StorageByAccount.Keys.Select(k => k.HexToByteArray()).ToList();
            Assert.Equal(4, accountsWithStorage.Count);

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 17,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = accountsWithStorage,
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(accountsWithStorage.Count, resp.Slots.Count);
            for (int i = 0; i < accountsWithStorage.Count; i++)
            {
                var expected = f.StorageByAccount[accountsWithStorage[i].ToHex()];
                Assert.Equal(expected.Count, resp.Slots[i].Count);
                for (int s = 0; s < expected.Count; s++)
                {
                    Assert.Equal(expected[s].slotHash.ToHex(), resp.Slots[i][s].Hash.ToHex());
                    Assert.Equal(expected[s].slotValue.ToHex(), resp.Slots[i][s].Data.ToHex());
                }
            }
        }

        [Fact]
        public async Task GetStorageRanges_OriginZero_WholeTrieServed_ReturnsEmptyProof()
        {
            var f = BuildFixture(8, withStorage: 4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var accountHash = f.StorageByAccount.Keys.First().HexToByteArray();

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 30,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = new List<byte[]> { accountHash },
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(f.StorageByAccount[accountHash.ToHex()].Count, resp.Slots[0].Count);
            Assert.Empty(resp.Proof);
        }

        [Fact]
        public async Task GetStorageRanges_NonOriginRequest_ReachesEndWithoutBudgetBreak_ReturnsNonEmptyProof()
        {
            var f = BuildFixture(8, withStorage: 4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var accountHash = f.StorageByAccount.Keys.First().HexToByteArray();
            var slots = f.StorageByAccount[accountHash.ToHex()];
            var midpoint = slots[slots.Count / 2].slotHash;

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 31,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = new List<byte[]> { accountHash },
                StartingHash = midpoint,
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL
            });

            var expectedFromMidpoint = slots.Where(s => ByteArrayComparer.Current.Compare(s.slotHash, midpoint) >= 0).ToList();
            Assert.Equal(expectedFromMidpoint.Count, resp.Slots[0].Count);
            Assert.NotEmpty(resp.Proof);

            var keys = resp.Slots[0].Select(s => s.Hash).ToList();
            var values = resp.Slots[0].Select(s => s.Data).ToList();
            var storageRoot = f.StorageRootByAccount[accountHash.ToHex()];
            var result = PatriciaRangeProofVerifier.Current.Verify(storageRoot, midpoint, keys, values, resp.Proof);
            Assert.True(result.Valid);
            Assert.False(result.HasMore);
        }

        [Fact]
        public async Task GetStorageRanges_PathKeyedStore_NonOriginRequest_ReturnsVerifiableNonEmptyProof()
        {
            var store = new InMemoryPathNodeStore();
            var bytecodes = new InMemoryBytecodeStore();
            var keccak = new Sha3Keccack();

            var accountHash = keccak.CalculateHash(new byte[] { 0xAA });
            var storageTrie = new PatriciaTrie(store, accountHash);
            var slots = new List<(byte[] slotHash, byte[] slotValue)>();
            for (int s = 0; s < 8; s++)
            {
                var slotHash = keccak.CalculateHash(new[] { (byte)s, (byte)0xBB });
                var slotValue = new byte[] { (byte)(0x80 | s), (byte)(s ^ 0x42) };
                storageTrie.Put(slotHash, slotValue);
                slots.Add((slotHash, slotValue));
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();
            slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.slotHash, b.slotHash));

            var stateTrie = new PatriciaTrie(store);
            var account = new Account
            {
                Nonce = (EvmUInt256)1U,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            };
            stateTrie.Put(accountHash, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();

            var handler = new PatriciaSnapRequestHandler(store, bytecodes);
            var midpoint = slots[slots.Count / 2].slotHash;

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 41,
                RootHash = stateTrie.Root.GetHash(),
                AccountHashes = new List<byte[]> { accountHash },
                StartingHash = midpoint,
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL
            });

            var expectedFromMidpoint = slots.Where(s => ByteArrayComparer.Current.Compare(s.slotHash, midpoint) >= 0).ToList();
            Assert.Equal(expectedFromMidpoint.Count, resp.Slots[0].Count);
            Assert.NotEmpty(resp.Proof);

            var keys = resp.Slots[0].Select(s => s.Hash).ToList();
            var values = resp.Slots[0].Select(s => s.Data).ToList();
            var result = PatriciaRangeProofVerifier.Current.Verify(storageRoot, midpoint, keys, values, resp.Proof);
            Assert.True(result.Valid);
            Assert.False(result.HasMore);
        }

        [Fact]
        public async Task GetStorageRanges_ByteBudget_StopsAndReturnsProof()
        {
            var f = BuildFixture(4, withStorage: 1, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var accountHash = f.StorageByAccount.Keys.First().HexToByteArray();

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 32,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = new List<byte[]> { accountHash },
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 40UL
            });

            var totalSlots = f.StorageByAccount[accountHash.ToHex()].Count;
            Assert.InRange(resp.Slots[0].Count, 1, totalSlots - 1);
            Assert.NotEmpty(resp.Proof);

            var keys = resp.Slots[0].Select(s => s.Hash).ToList();
            var values = resp.Slots[0].Select(s => s.Data).ToList();
            var storageRoot = f.StorageRootByAccount[accountHash.ToHex()];
            var result = PatriciaRangeProofVerifier.Current.Verify(storageRoot, new byte[32], keys, values, resp.Proof);
            Assert.True(result.Valid);
            Assert.True(result.HasMore);
        }

        [Fact]
        public async Task GetTrieNodes_EmptyPath_ReturnsRootNode()
        {
            var f = BuildFixture(16);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var rootHash = f.StateTrie.Root.GetHash();
            var hashProvider = new Sha3KeccackHashProvider();

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 23,
                RootHash = rootHash,
                Paths = new List<List<byte[]>>
                {
                    new List<byte[]> { new byte[] { 0x00 } },
                    new List<byte[]> { new byte[] { 0x00 } }
                },
                ResponseBytes = 1_000_000UL
            });

            Assert.Equal(2, resp.Nodes.Count);
            foreach (var node in resp.Nodes)
            {
                Assert.NotEmpty(node);
                Assert.Equal(rootHash.ToHex(), hashProvider.ComputeHash(node).ToHex());
            }
        }

        [Fact]
        public async Task GetTrieNodes_EmptyPathset_ThrowsSnapMalformedRequestException()
        {
            var f = BuildFixture(8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            await Assert.ThrowsAsync<PatriciaSnapRequestHandler.SnapMalformedRequestException>(() =>
                handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 24,
                    RootHash = f.StateTrie.Root.GetHash(),
                    Paths = new List<List<byte[]>>
                    {
                        new List<byte[]>(),
                        new List<byte[]> { new byte[] { 0x00 } }
                    },
                    ResponseBytes = 1_000_000UL
                }));
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }
    }
}
