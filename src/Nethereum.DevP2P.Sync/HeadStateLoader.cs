using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Loads a JSON state dump (the <c>headstate.json</c> format produced by
    /// reference clients during conformance testing) into a Patricia state trie
    /// + per-account storage tries + a bytecode store. Returns the computed
    /// root so the caller can assert it equals the dump's claimed root, which
    /// canaries that our trie encoding is byte-exact against the reference.
    ///
    /// This is the prerequisite for serving snap/1 against the same testdata
    /// chain that <c>devp2p rlpx snap-test</c> validates against.
    /// </summary>
    public static class HeadStateLoader
    {
        public class LoadResult
        {
            public PatriciaTrie StateTrie { get; set; }
            public InMemoryTrieStorage TrieStorage { get; set; }
            public BytecodeStore Bytecodes { get; set; }
            public byte[] ComputedRoot { get; set; }
            public byte[] ExpectedRoot { get; set; }
            public bool RootMatches => ByteUtil.AreEqual(ComputedRoot, ExpectedRoot);
            public int AccountCount { get; set; }
        }

        public class BytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public void Put(byte[] codeHash, byte[] code) { _codes[codeHash] = code; }
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash, out var v) ? v : null;
            public int Count => _codes.Count;
        }

        public static LoadResult Load(string headStateJsonPath)
        {
            if (!File.Exists(headStateJsonPath))
                throw new FileNotFoundException($"headstate.json not found at {headStateJsonPath}");

            var doc = JObject.Parse(File.ReadAllText(headStateJsonPath));
            var expectedRoot = ParseHex(doc["root"]?.ToString() ?? throw new InvalidOperationException("headstate.json missing 'root'"));

            var storage = new InMemoryTrieStorage();
            var stateTrie = new PatriciaTrie();
            var codes = new BytecodeStore();

            var accounts = (JObject)doc["accounts"];
            int count = 0;
            foreach (var prop in accounts.Properties())
            {
                var entry = (JObject)prop.Value;
                count++;

                var accountKey = ParseHex(entry["key"].ToString());
                var balance = BigInteger.Parse(entry["balance"].ToString());
                var nonce = entry["nonce"].ToObject<ulong>();

                var storageRoot = DefaultValues.EMPTY_TRIE_HASH;
                if (entry["storage"] is JObject slots && slots.Count > 0)
                {
                    var keccak = new Sha3KeccackHashProvider();
                    var storageTrie = new PatriciaTrie();
                    foreach (var slotProp in slots.Properties())
                    {
                        // JSON storage keys are RAW slot indices padded to 32
                        // bytes (Geth's state.Dump unhashes via preimages).
                        // The MPT key is keccak256 of the raw slot index.
                        var rawSlotKey = ParseHex(slotProp.Name);
                        var trieKey = keccak.ComputeHash(rawSlotKey);
                        var slotValue = ParseHex(slotProp.Value.ToString());
                        // Storage values in the trie are RLP-encoded with
                        // leading zeros stripped from the big-endian integer.
                        var stripped = slotValue.TrimZeroBytes();
                        var rlpEncoded = RLP.RLP.EncodeElement(stripped);
                        storageTrie.Put(trieKey, rlpEncoded, storage);
                    }
                    storageTrie.SaveDirtyNodesToStorage(storage);
                    storageRoot = storageTrie.Root.GetHash();
                }

                var codeHash = ParseHex(entry["codeHash"].ToString());
                if (entry["code"] != null)
                {
                    var code = ParseHex(entry["code"].ToString());
                    codes.Put(codeHash, code);
                }

                var account = new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = (EvmUInt256)balance,
                    StateRoot = storageRoot,
                    CodeHash = codeHash
                };
                stateTrie.Put(accountKey, new AccountEncoder().Encode(account), storage);
            }
            stateTrie.SaveDirtyNodesToStorage(storage);

            return new LoadResult
            {
                StateTrie = stateTrie,
                TrieStorage = storage,
                Bytecodes = codes,
                ComputedRoot = stateTrie.Root.GetHash(),
                ExpectedRoot = expectedRoot,
                AccountCount = count
            };
        }

        private static byte[] ParseHex(string s)
        {
            if (s == null) return null;
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            if (s.Length == 0) return new byte[0];
            if (s.Length % 2 != 0) s = "0" + s;
            return s.HexToByteArray();
        }

    }
}
