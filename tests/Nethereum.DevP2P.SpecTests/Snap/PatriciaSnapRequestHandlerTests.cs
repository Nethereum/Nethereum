using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    /// <summary>
    /// End-to-end tests for PatriciaSnapRequestHandler. Builds a synthetic
    /// state trie with mixed accounts (some with storage, some with code,
    /// some plain) and invokes the four snap/1 request types directly,
    /// checking the responses' structure and verifiability.
    ///
    /// These are the same code paths the go-ethereum `devp2p rlpx snap-test`
    /// conformance suite will exercise once the snap/1 wire dispatcher is
    /// wired into an RLPx multiplexed session.
    /// </summary>
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
            public InMemoryTrieStorage TrieStorage;
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
            f.TrieStorage = new InMemoryTrieStorage();
            f.Bytecodes = new InMemoryBytecodeStore();
            f.StateTrie = new PatriciaTrie();

            for (int i = 0; i < accountCount; i++)
            {
                var addrSeed = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff), (byte)0xAA });

                byte[] storageRoot = DefaultValues.EMPTY_TRIE_HASH;
                if (i < withStorage)
                {
                    var storageTrie = new PatriciaTrie();
                    var slots = new List<(byte[], byte[])>();
                    for (int s = 0; s < 16; s++)
                    {
                        var slotHash = keccak.CalculateHash(new[] { (byte)i, (byte)s, (byte)0xBB });
                        var slotValue = new byte[] { (byte)(0x80 | (s & 0x7f)), (byte)(s ^ 0x42) };
                        storageTrie.Put(slotHash, slotValue, f.TrieStorage);
                        slots.Add((slotHash, slotValue));
                    }
                    storageTrie.SaveDirtyNodesToStorage(f.TrieStorage);
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
                f.StateTrie.Put(addrSeed, body, f.TrieStorage);
                f.Accounts.Add((addrSeed, body, storageRoot));
            }
            f.StateTrie.SaveDirtyNodesToStorage(f.TrieStorage);
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
                // Response bodies are slim-encoded (snap/1 spec). Compare against
                // the slim form of the canonical body, not the canonical itself.
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
            var proofStorage = new InMemoryTrieStorage();
            foreach (var node in resp.Proof) proofStorage.Put(hashProvider.ComputeHash(node), node);
            var verifyTrie = new PatriciaTrie(rootHash);
            // Trie lookups return CANONICAL bodies; response carries SLIM ones.
            Assert.Equal(SlimAccountEncoder.ToSlim(verifyTrie.Get(startKey, proofStorage)).ToHex(),
                         resp.Accounts.First().Body.ToHex());
            Assert.Equal(SlimAccountEncoder.ToSlim(new PatriciaTrie(rootHash).Get(limit, proofStorage)).ToHex(),
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

            // Collect codeHashes from the accounts' bodies.
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
            // Per Geth's chain.ContractCodeWithPrefix: unknown code hashes
            // (random/state-root values, etc.) are silently skipped — the
            // server does NOT return an empty placeholder. Only the special
            // case EMPTY_DATA_HASH (keccak256("")) returns empty bytes.
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
            // EMPTY_DATA_HASH (keccak256("")) is the special case: the server
            // must return one empty-bytes code per occurrence in the request.
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
        public async Task GetTrieNodes_EmptyPath_ReturnsRootNode()
        {
            // Paths in snap/1 GetTrieNodes are compact-hex-encoded prefixes
            // of the trie's nibble trajectory — not raw account-hash keys.
            // Path = compact-encoded empty (single byte 0x00) → root node.
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
        public async Task GetTrieNodes_EmptyPathset_ReturnsEmptyResponse()
        {
            // Per Geth's ServiceGetTrieNodesQuery: a zero-item pathset is a
            // protocol error. We surface as an empty response.
            var f = BuildFixture(8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 24,
                RootHash = f.StateTrie.Root.GetHash(),
                Paths = new List<List<byte[]>>
                {
                    new List<byte[]>(), // empty pathset
                    new List<byte[]> { new byte[] { 0x00 } }
                },
                ResponseBytes = 1_000_000UL
            });

            Assert.Empty(resp.Nodes);
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }
    }
}
