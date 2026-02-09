using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Patricia
{
    public class ComprehensiveGethTrieTests
    {
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        [Theory]
        [MemberData(nameof(GethTrieTestLoader.GetPlainTrieTestCases), MemberType = typeof(GethTrieTestLoader))]
        public void PlainTrieTest(GethTrieTestLoader.TrieTestCase testCase)
        {
            var trie = new PatriciaTrie();

            foreach (var (key, value) in testCase.Operations)
            {
                if (value == null)
                {
                    trie.Delete(key);
                }
                else
                {
                    trie.Put(key, value);
                }
            }

            var actualRoot = trie.Root.GetHash();
            Assert.True(
                testCase.ExpectedRoot.AreTheSame(actualRoot),
                $"Test '{testCase}' failed.\nExpected: {testCase.ExpectedRoot.ToHex()}\nActual:   {actualRoot.ToHex()}");
        }

        [Theory]
        [MemberData(nameof(GethTrieTestLoader.GetSecureTrieTestCases), MemberType = typeof(GethTrieTestLoader))]
        public void SecureTrieTest(GethTrieTestLoader.TrieTestCase testCase)
        {
            var trie = new PatriciaTrie();

            foreach (var (key, value) in testCase.Operations)
            {
                var hashedKey = _keccak.CalculateHash(key);

                if (value == null)
                {
                    trie.Delete(hashedKey);
                }
                else
                {
                    trie.Put(hashedKey, value);
                }
            }

            var actualRoot = trie.Root.GetHash();
            Assert.True(
                testCase.ExpectedRoot.AreTheSame(actualRoot),
                $"Test '{testCase}' failed.\nExpected: {testCase.ExpectedRoot.ToHex()}\nActual:   {actualRoot.ToHex()}");
        }

        [Fact]
        public void DirtyFlag_NewNodeStartsDirty()
        {
            var trie = new PatriciaTrie();
            trie.Put(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes("value"));

            Assert.True(trie.Root.IsDirty);
        }

        [Fact]
        public void DirtyFlag_BecomesCleanAfterGetHash()
        {
            var trie = new PatriciaTrie();
            trie.Put(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes("value"));

            Assert.True(trie.Root.IsDirty);

            var hash = trie.Root.GetHash();

            Assert.False(trie.Root.IsDirty);
        }

        [Fact]
        public void DirtyFlag_BecomesDirtyAfterModification()
        {
            var trie = new PatriciaTrie();
            trie.Put(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes("value"));

            var hash1 = trie.Root.GetHash();
            Assert.False(trie.Root.IsDirty);

            trie.Put(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes("newvalue"));
            Assert.True(trie.Root.IsDirty);

            var hash2 = trie.Root.GetHash();
            Assert.False(trie.Root.IsDirty);
            Assert.False(hash1.AreTheSame(hash2));
        }

        [Fact]
        public void DirtyFlag_CachedHashReturnedWhenClean()
        {
            var trie = new PatriciaTrie();
            trie.Put(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1"));
            trie.Put(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2"));

            var hash1 = trie.Root.GetHash();
            var hash2 = trie.Root.GetHash();

            Assert.Same(hash1, hash2);
        }

        [Fact]
        public void IncrementalUpdate_MatchesFreshBuild()
        {
            var trie = new PatriciaTrie();

            trie.Put(UTF8("do"), UTF8("verb"));
            trie.Put(UTF8("dog"), UTF8("puppy"));
            trie.Put(UTF8("doge"), UTF8("coin"));
            var root1 = trie.Root.GetHash().ToHex();

            trie.Put(UTF8("dog"), UTF8("cat"));
            var incrementalRoot = trie.Root.GetHash().ToHex();

            var freshTrie = new PatriciaTrie();
            freshTrie.Put(UTF8("do"), UTF8("verb"));
            freshTrie.Put(UTF8("dog"), UTF8("cat"));
            freshTrie.Put(UTF8("doge"), UTF8("coin"));
            var freshRoot = freshTrie.Root.GetHash().ToHex();

            Assert.Equal(freshRoot, incrementalRoot);
        }

        [Fact]
        public void IncrementalDelete_MatchesFreshBuild()
        {
            var trie = new PatriciaTrie();

            trie.Put(UTF8("do"), UTF8("verb"));
            trie.Put(UTF8("dog"), UTF8("puppy"));
            trie.Put(UTF8("doge"), UTF8("coin"));
            var root1 = trie.Root.GetHash();

            trie.Delete(UTF8("dog"));
            var incrementalRoot = trie.Root.GetHash().ToHex();

            var freshTrie = new PatriciaTrie();
            freshTrie.Put(UTF8("do"), UTF8("verb"));
            freshTrie.Put(UTF8("doge"), UTF8("coin"));
            var freshRoot = freshTrie.Root.GetHash().ToHex();

            Assert.Equal(freshRoot, incrementalRoot);
        }

        [Fact]
        public void MultipleUpdates_AllMatchFreshBuild()
        {
            var operations = new List<(string, string)>
            {
                ("key1", "val1"),
                ("key2", "val2"),
                ("key3", "val3"),
                ("key1", "updated1"),
                ("key4", "val4"),
                ("key2", null),
                ("key5", "val5"),
                ("key3", "updated3"),
            };

            var incrementalTrie = new PatriciaTrie();
            foreach (var (key, value) in operations)
            {
                if (value == null)
                    incrementalTrie.Delete(UTF8(key));
                else
                    incrementalTrie.Put(UTF8(key), UTF8(value));
            }
            var incrementalRoot = incrementalTrie.Root.GetHash().ToHex();

            var freshTrie = new PatriciaTrie();
            freshTrie.Put(UTF8("key1"), UTF8("updated1"));
            freshTrie.Put(UTF8("key3"), UTF8("updated3"));
            freshTrie.Put(UTF8("key4"), UTF8("val4"));
            freshTrie.Put(UTF8("key5"), UTF8("val5"));
            var freshRoot = freshTrie.Root.GetHash().ToHex();

            Assert.Equal(freshRoot, incrementalRoot);
        }

        [Fact]
        public void SecureTrie_IncrementalUpdate_MatchesFreshBuild()
        {
            var trie = new PatriciaTrie();

            trie.Put(HashKey("account1"), UTF8("balance1"));
            trie.Put(HashKey("account2"), UTF8("balance2"));
            trie.Put(HashKey("account3"), UTF8("balance3"));
            var root1 = trie.Root.GetHash();

            trie.Put(HashKey("account2"), UTF8("newbalance2"));
            var incrementalRoot = trie.Root.GetHash().ToHex();

            var freshTrie = new PatriciaTrie();
            freshTrie.Put(HashKey("account1"), UTF8("balance1"));
            freshTrie.Put(HashKey("account2"), UTF8("newbalance2"));
            freshTrie.Put(HashKey("account3"), UTF8("balance3"));
            var freshRoot = freshTrie.Root.GetHash().ToHex();

            Assert.Equal(freshRoot, incrementalRoot);
        }

        [Fact]
        public void Performance_IncrementalUpdateIsFasterThanRebuild()
        {
            var keyCount = 1000;
            var keys = new List<byte[]>();
            for (int i = 0; i < keyCount; i++)
            {
                keys.Add(_keccak.CalculateHash(BitConverter.GetBytes(i)));
            }

            var trie = new PatriciaTrie();
            for (int i = 0; i < keyCount; i++)
            {
                trie.Put(keys[i], BitConverter.GetBytes(i));
            }
            var initialRoot = trie.Root.GetHash();

            var swIncremental = Stopwatch.StartNew();
            trie.Put(keys[500], new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
            var incrementalRoot = trie.Root.GetHash();
            swIncremental.Stop();

            var freshTrie = new PatriciaTrie();
            var swFresh = Stopwatch.StartNew();
            for (int i = 0; i < keyCount; i++)
            {
                if (i == 500)
                    freshTrie.Put(keys[i], new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                else
                    freshTrie.Put(keys[i], BitConverter.GetBytes(i));
            }
            var freshRoot = freshTrie.Root.GetHash();
            swFresh.Stop();

            Assert.Equal(freshRoot.ToHex(), incrementalRoot.ToHex());
            Assert.True(swIncremental.ElapsedMilliseconds <= swFresh.ElapsedMilliseconds,
                $"Incremental ({swIncremental.ElapsedMilliseconds}ms) should be faster than fresh build ({swFresh.ElapsedMilliseconds}ms)");
        }

        [Fact]
        public void LargeTrie_IncrementalUpdateStillCorrect()
        {
            var keyCount = 5000;
            var keys = new List<byte[]>();
            for (int i = 0; i < keyCount; i++)
            {
                keys.Add(_keccak.CalculateHash(BitConverter.GetBytes(i)));
            }

            var trie = new PatriciaTrie();
            for (int i = 0; i < keyCount; i++)
            {
                trie.Put(keys[i], BitConverter.GetBytes(i));
            }
            var initialRoot = trie.Root.GetHash();

            for (int update = 0; update < 10; update++)
            {
                var idx = (update * 500) % keyCount;
                var newValue = BitConverter.GetBytes(idx + 100000);
                trie.Put(keys[idx], newValue);
            }
            var incrementalRoot = trie.Root.GetHash();

            var freshTrie = new PatriciaTrie();
            for (int i = 0; i < keyCount; i++)
            {
                var value = BitConverter.GetBytes(i);
                for (int update = 0; update < 10; update++)
                {
                    var idx = (update * 500) % keyCount;
                    if (i == idx)
                        value = BitConverter.GetBytes(idx + 100000);
                }
                freshTrie.Put(keys[i], value);
            }
            var freshRoot = freshTrie.Root.GetHash();

            Assert.Equal(freshRoot.ToHex(), incrementalRoot.ToHex());
        }

        [Fact]
        public void EmptyTrie_HasCorrectRoot()
        {
            var trie = new PatriciaTrie();
            var emptyRoot = trie.Root.GetHash();

            var expectedEmptyRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421";
            Assert.Equal(expectedEmptyRoot, emptyRoot.ToHex());
        }

        [Fact]
        public void DeleteAll_ReturnsToEmptyRoot()
        {
            var trie = new PatriciaTrie();

            trie.Put(UTF8("key1"), UTF8("value1"));
            trie.Put(UTF8("key2"), UTF8("value2"));
            var midRoot = trie.Root.GetHash();

            trie.Delete(UTF8("key1"));
            trie.Delete(UTF8("key2"));
            var finalRoot = trie.Root.GetHash();

            var expectedEmptyRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421";
            Assert.Equal(expectedEmptyRoot, finalRoot.ToHex());
        }

        private static byte[] UTF8(string s) => Encoding.UTF8.GetBytes(s);

        private byte[] HashKey(string key) => _keccak.CalculateHash(Encoding.UTF8.GetBytes(key));
    }
}
