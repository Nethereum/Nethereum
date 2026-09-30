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
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static class HeadStateLoader
    {
        public class LoadResult
        {
            public PatriciaTrie StateTrie { get; set; }
            public InMemoryContentNodeStore TrieStorage { get; set; }
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

            var storage = new InMemoryContentNodeStore();
            var stateTrie = new PatriciaTrie(storage);
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
                    var keccak = Sha3KeccackHashProvider.Instance;
                    var storageTrie = new PatriciaTrie(storage);
                    foreach (var slotProp in slots.Properties())
                    {
                        var rawSlotKey = ParseHex(slotProp.Name);
                        var trieKey = keccak.ComputeHash(rawSlotKey);
                        var slotValue = ParseHex(slotProp.Value.ToString());
                        var stripped = slotValue.TrimZeroBytes();
                        var rlpEncoded = RLP.RLP.EncodeElement(stripped);
                        storageTrie.Put(trieKey, rlpEncoded);
                    }
                    storageTrie.SaveDirtyNodesToStorage();
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
                stateTrie.Put(accountKey, new AccountEncoder().Encode(account));
            }
            stateTrie.SaveDirtyNodesToStorage();

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
