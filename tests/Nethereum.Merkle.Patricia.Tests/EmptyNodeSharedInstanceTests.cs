using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class EmptyNodeSharedInstanceTests
    {
        private const string EmptyTrieRootHex = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421";

        private static byte[] KeyHash(int i) => new Sha3Keccack().CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
        private static byte[] Value(int i) => new byte[] { (byte)(0x80 | (i & 0x7f)), (byte)(i ^ 0x42), (byte)i, (byte)(i >> 3) };


        [Fact]
        public void RootEquivalence_FreshVsSharedEmptyRoot_AcrossPutAndDelete()
        {
            var trieA = new PatriciaTrie(new EmptyNode());
            var trieB = new PatriciaTrie();

            Assert.Equal(trieA.Root.GetHash().ToHex(), trieB.Root.GetHash().ToHex());

            var keys = new List<byte[]>();
            for (int i = 0; i < 600; i++)
            {
                var key = KeyHash(i);
                keys.Add(key);
                var value = Value(i);
                trieA.Put(key, value);
                trieB.Put(key, value);
                Assert.Equal(trieA.Root.GetHash().ToHex(), trieB.Root.GetHash().ToHex());
            }

            var rnd = new Random(20260812);
            var order = Enumerable.Range(0, keys.Count).OrderBy(_ => rnd.Next()).ToList();
            foreach (var idx in order)
            {
                trieA.Delete(keys[idx]);
                trieB.Delete(keys[idx]);
                Assert.Equal(trieA.Root.GetHash().ToHex(), trieB.Root.GetHash().ToHex());
            }

            Assert.Equal(EmptyTrieRootHex, trieA.Root.GetHash().ToHex());
            Assert.Equal(EmptyTrieRootHex, trieB.Root.GetHash().ToHex());
        }

        [Fact]
        public void RootEquivalence_NodeLevel_EmptyChildSlot_FreshVsShared()
        {
            Node BuildBranch(Func<Node> emptyFactory)
            {
                var branch = new BranchNode();
                var leaf = new LeafNode { Nibbles = new byte[] { 1, 2, 3 }, Value = new byte[] { 0xAB, 0xCD } };
                for (int i = 0; i < 16; i++)
                    branch.SetChild(i, i == 7 ? leaf : emptyFactory());
                return branch;
            }

            var fresh = BuildBranch(() => new EmptyNode());
            var shared = BuildBranch(() => EmptyNode.Instance);

            Assert.Equal(fresh.GetEncodedData().ToHex(), shared.GetEncodedData().ToHex());
            Assert.Equal(fresh.GetHash().ToHex(), shared.GetHash().ToHex());
        }


        [Fact]
        public void DeleteAll_CollapsesRootToWellKnownEmptyTrieHash()
        {
            var trie = new PatriciaTrie();
            var keys = new List<byte[]>();
            for (int i = 0; i < 400; i++)
            {
                var key = KeyHash(i);
                keys.Add(key);
                trie.Put(key, Value(i));
            }
            Assert.NotEqual(EmptyTrieRootHex, trie.Root.GetHash().ToHex());

            foreach (var key in keys) trie.Delete(key);

            Assert.Equal(EmptyTrieRootHex, trie.Root.GetHash().ToHex());
            Assert.Same(EmptyNode.Instance, trie.Root);
        }

        [Fact]
        public void BranchWith15Of16EmptyChildren_ValuePlusOneChild_MatchesIndependentEncoding()
        {
            var leaf = new LeafNode { Nibbles = new byte[] { 9 }, Value = new byte[] { 0x11, 0x22 } };
            var branch = new BranchNode();
            for (int i = 0; i < 16; i++)
                branch.SetChild(i, i == 3 ? leaf : EmptyNode.Instance);
            branch.Value = new byte[] { 0x77 };

            Assert.Equal(15, branch.Children.Count(c => c is EmptyNode));
            Assert.Same(EmptyNode.Instance, branch.Children[0]);
            Assert.Same(EmptyNode.Instance, branch.Children[15]);

            var items = new List<byte[]>();
            for (int i = 0; i < 16; i++)
                items.Add(i == 3 ? leaf.GetEncodedData() : Nethereum.RLP.RLP.EncodeElement(new byte[0]));
            items.Add(Nethereum.RLP.RLP.EncodeElement(branch.Value));
            var expectedEncoding = Nethereum.RLP.RLP.EncodeList(items.ToArray());

            Assert.Equal(expectedEncoding.ToHex(), branch.GetEncodedData().ToHex());
        }

        [Fact]
        public void NestedExtendedThenEmpty_DeletingSharedPrefixSiblings_CollapsesThroughEmptyToSurvivorRoot()
        {
            var keccak = new Sha3Keccack();
            byte[] MakeKey(byte tag) => keccak.CalculateHash(new byte[] { 0xAA, 0xAA, 0xAA, tag });

            byte[] Base(byte last) => Enumerable.Repeat((byte)0x5A, 31).Append(last).ToArray();
            var keyA = Base(0x01);
            var keyB = Base(0x02);
            var keyC = MakeKey(0xFF);

            var trie = new PatriciaTrie();
            trie.Put(keyA, new byte[] { 0x01 });
            trie.Put(keyB, new byte[] { 0x02 });
            trie.Put(keyC, new byte[] { 0x03 });

            trie.Delete(keyA);
            trie.Delete(keyB);

            var survivorOnly = new PatriciaTrie();
            survivorOnly.Put(keyC, new byte[] { 0x03 });

            Assert.Equal(survivorOnly.Root.GetHash().ToHex(), trie.Root.GetHash().ToHex());
        }

        [Fact]
        public void SubtreeClear_SelfDestructStyleDeleteOfWholePrefixGroup_MatchesFreshTrieOfSurvivors()
        {
            var trie = new PatriciaTrie();
            var cleared = new List<byte[]>();

            for (int i = 0; i < 250; i++)
                trie.Put(KeyHash(i), Value(i));

            for (int i = 0; i < 40; i++)
            {
                var key = Enumerable.Repeat((byte)0xC3, 31).Append((byte)i).ToArray();
                trie.Put(key, new byte[] { (byte)(i + 1), 0x99 });
                cleared.Add(key);
            }

            foreach (var key in cleared) trie.Delete(key);

            var survivorsOnly = new PatriciaTrie();
            for (int i = 0; i < 250; i++) survivorsOnly.Put(KeyHash(i), Value(i));

            Assert.Equal(survivorsOnly.Root.GetHash().ToHex(), trie.Root.GetHash().ToHex());
        }


        [Fact]
        public void SharedInstance_NotMutated_AfterThousandsOfPutDeleteOps()
        {
            var expectedHash = EmptyNode.Instance.GetHash();
            var expectedEncoded = EmptyNode.Instance.GetEncodedData();

            var rnd = new Random(7);
            var tries = new List<PatriciaTrie> { new PatriciaTrie(), new PatriciaTrie(), new PatriciaTrie() };
            var liveKeys = new List<List<byte[]>> { new List<byte[]>(), new List<byte[]>(), new List<byte[]>() };

            for (int op = 0; op < 6000; op++)
            {
                var t = op % tries.Count;
                if (rnd.Next(0, 3) == 0 && liveKeys[t].Count > 0)
                {
                    var idx = rnd.Next(liveKeys[t].Count);
                    tries[t].Delete(liveKeys[t][idx]);
                    liveKeys[t].RemoveAt(idx);
                }
                else
                {
                    var key = KeyHash(op);
                    tries[t].Put(key, Value(op));
                    liveKeys[t].Add(key);
                }
            }

            Assert.Null(EmptyNode.Instance.Owner);
            Assert.Null(EmptyNode.Instance.Path);
            Assert.Equal(expectedHash.ToHex(), EmptyNode.Instance.GetHash().ToHex());
            Assert.Equal(expectedEncoded.ToHex(), EmptyNode.Instance.GetEncodedData().ToHex());
            Assert.Equal(EmptyTrieRootHex, EmptyNode.Instance.GetHash().ToHex());
        }


        [Fact]
        public void EncodingConstancy_SharedInstance_MatchesFreshEmptyNode_AndRawRlpEmptyString()
        {
            var rawRlpEmpty = Nethereum.RLP.RLP.EncodeElement(new byte[0]);

            Assert.Equal(rawRlpEmpty.ToHex(), EmptyNode.Instance.GetEncodedData().ToHex());
            Assert.Equal(rawRlpEmpty.ToHex(), new EmptyNode().GetEncodedData().ToHex());

            var expectedHash = Sha3KeccackHashProvider.Instance.ComputeHash(rawRlpEmpty);
            Assert.Equal(expectedHash.ToHex(), EmptyNode.Instance.GetHash().ToHex());
            Assert.Equal(expectedHash.ToHex(), new EmptyNode().GetHash().ToHex());
            Assert.Equal(EmptyTrieRootHex, expectedHash.ToHex());
        }


        [Fact]
        public void Concurrency_EightPlusThreads_IndependentTriesShareInstance_RootsCorrect()
        {
            const int threadCount = 12;
            var expected = new byte[threadCount][];
            var actual = new byte[threadCount][];

            Parallel.For(0, threadCount, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, t =>
            {
                var trie = new PatriciaTrie();
                var keys = new List<byte[]>();
                for (int i = 0; i < 120; i++)
                {
                    var key = new Sha3Keccack().CalculateHash(new byte[] { (byte)t, (byte)(i >> 8), (byte)(i & 0xff) });
                    keys.Add(key);
                    trie.Put(key, Value(i));
                }

                var referenceInOrder = new PatriciaTrie();
                for (int i = 0; i < 120; i++)
                {
                    var key = new Sha3Keccack().CalculateHash(new byte[] { (byte)t, (byte)(i >> 8), (byte)(i & 0xff) });
                    referenceInOrder.Put(key, Value(i));
                }

                for (int i = 0; i < 60; i++) trie.Delete(keys[i]);
                for (int i = 0; i < 60; i++) referenceInOrder.Delete(keys[i]);

                actual[t] = trie.Root.GetHash();
                expected[t] = referenceInOrder.Root.GetHash();
            });

            for (int t = 0; t < threadCount; t++)
                Assert.Equal(expected[t].ToHex(), actual[t].ToHex());
        }

        [Fact]
        public void Concurrency_GetHashAndGetEncodedData_OnSharedInstance_AreRaceSafe()
        {
            var expectedHash = Sha3KeccackHashProvider.Instance.ComputeHash(Nethereum.RLP.RLP.EncodeElement(new byte[0]));
            var expectedEncoded = Nethereum.RLP.RLP.EncodeElement(new byte[0]);

            const int threadCount = 16;
            var hashResults = new byte[threadCount][];
            var encodedResults = new byte[threadCount][];
            var barrier = new Barrier(threadCount);

            var threads = new Thread[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                int local = t;
                threads[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < 500; i++)
                    {
                        hashResults[local] = EmptyNode.Instance.GetHash();
                        encodedResults[local] = EmptyNode.Instance.GetEncodedData();
                    }
                });
                threads[t].Start();
            }
            foreach (var th in threads) th.Join();

            for (int t = 0; t < threadCount; t++)
            {
                Assert.Equal(expectedHash.ToHex(), hashResults[t].ToHex());
                Assert.Equal(expectedEncoded.ToHex(), encodedResults[t].ToHex());
            }
        }
    }
}
