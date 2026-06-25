using System.Collections.Generic;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.UnitTests.Sync
{
    /// <summary>
    /// Synthetic state fixture shared by snap/1 server-hardening tests
    /// (<see cref="PatriciaSnapRequestHandler"/>). Builds a Patricia state trie
    /// with N accounts (sorted by key hash) and exposes the matching root +
    /// trie storage + bytecode store.
    /// </summary>
    internal static class SnapHandlerTestFixture
    {
        public sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public void Put(byte[] codeHash, byte[] code) { _codes[codeHash] = code; }
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash, out var v) ? v : null;
        }

        public sealed class Fixture
        {
            public PatriciaTrie StateTrie;
            public InMemoryTrieStorage TrieStorage;
            public InMemoryBytecodeStore Bytecodes;
            public List<(byte[] hash, byte[] body)> Accounts = new();
            public List<byte[]> KnownCodeHashes = new();
        }

        public static Fixture Build(int accountCount = 64, int withCode = 16)
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

                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (i < withCode)
                {
                    var code = new byte[] { (byte)0x60, (byte)(i & 0xff), (byte)0xFF };
                    codeHash = hashProvider.ComputeHash(code);
                    f.Bytecodes.Put(codeHash, code);
                    f.KnownCodeHashes.Add(codeHash);
                }

                var account = new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)((ulong)(i + 1) * 1_000UL),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = codeHash
                };
                var body = new AccountEncoder().Encode(account);
                f.StateTrie.Put(addrSeed, body, f.TrieStorage);
                f.Accounts.Add((addrSeed, body));
            }
            f.StateTrie.SaveDirtyNodesToStorage(f.TrieStorage);
            f.Accounts.Sort((a, b) => ByteArrayComparer.Current.Compare(a.hash, b.hash));
            return f;
        }

        public static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }
    }
}
