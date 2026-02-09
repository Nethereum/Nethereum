using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Patricia
{
    public class GethTrieTests
    {
        [Theory]
        [MemberData(nameof(GethTrieTestVectors.GetTestCases), MemberType = typeof(GethTrieTestVectors))]
        public void GethTrieTestVector(GethTrieTestVectors.TrieTestCase testCase)
        {
            var trie = new PatriciaTrie();

            foreach (var (key, value) in testCase.Operations)
            {
                var keyBytes = ConvertKeyToBytes(key);

                if (value == null)
                {
                    trie.Delete(keyBytes);
                }
                else
                {
                    var valueBytes = ConvertValueToBytes(value);
                    trie.Put(keyBytes, valueBytes);
                }
            }

            var rootHash = trie.Root.GetHash().ToHex();
            Assert.True(
                testCase.ExpectedRoot.IsTheSameHex(rootHash),
                $"Test '{testCase.Name}' failed. Expected root: {testCase.ExpectedRoot}, Actual: {rootHash}");
        }

        [Fact]
        public void EmptyValuesTest()
        {
            RunSingleTest(GethTrieTestVectors.GetEmptyValuesTest());
        }

        [Fact]
        public void BranchingTest()
        {
            RunSingleTest(GethTrieTestVectors.GetBranchingTest());
        }

        [Fact]
        public void JeffTest()
        {
            RunSingleTest(GethTrieTestVectors.GetJeffTest());
        }

        [Fact]
        public void InsertMiddleLeafTest()
        {
            RunSingleTest(GethTrieTestVectors.GetInsertMiddleLeafTest());
        }

        [Fact]
        public void BranchValueUpdateTest()
        {
            RunSingleTest(GethTrieTestVectors.GetBranchValueUpdateTest());
        }

        private void RunSingleTest(GethTrieTestVectors.TrieTestCase testCase)
        {
            var trie = new PatriciaTrie();

            foreach (var (key, value) in testCase.Operations)
            {
                var keyBytes = ConvertKeyToBytes(key);

                if (value == null)
                {
                    trie.Delete(keyBytes);
                }
                else
                {
                    var valueBytes = ConvertValueToBytes(value);
                    trie.Put(keyBytes, valueBytes);
                }
            }

            var rootHash = trie.Root.GetHash().ToHex();
            Assert.True(
                testCase.ExpectedRoot.IsTheSameHex(rootHash),
                $"Test '{testCase.Name}' failed. Expected root: {testCase.ExpectedRoot}, Actual: {rootHash}");
        }

[Fact]
        public void SimpleDeleteTest()
        {
            var trie = new PatriciaTrie();

            trie.Put(Encoding.UTF8.GetBytes("do"), Encoding.UTF8.GetBytes("verb"));
            trie.Put(Encoding.UTF8.GetBytes("dog"), Encoding.UTF8.GetBytes("puppy"));

            var rootBeforeDelete = trie.Root.GetHash().ToHex();

            trie.Delete(Encoding.UTF8.GetBytes("dog"));

            var rootAfterDelete = trie.Root.GetHash().ToHex();

            var trie2 = new PatriciaTrie();
            trie2.Put(Encoding.UTF8.GetBytes("do"), Encoding.UTF8.GetBytes("verb"));
            var expectedRoot = trie2.Root.GetHash().ToHex();

            Assert.True(expectedRoot.IsTheSameHex(rootAfterDelete),
                $"After deleting 'dog', trie should match trie with only 'do'. Expected: {expectedRoot}, Actual: {rootAfterDelete}");
        }

        [Fact]
        public void DeleteAllShouldReturnEmptyRoot()
        {
            var trie = new PatriciaTrie();

            trie.Put(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1"));
            trie.Put(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2"));

            var rootAfterPut = trie.Root.GetType().Name;
            var innerAfterPut = (trie.Root as ExtendedNode)?.InnerNode?.GetType().Name ?? "N/A";
            var branchChildrenAfterPut = GetBranchChildCount((trie.Root as ExtendedNode)?.InnerNode as BranchNode);

            trie.Delete(Encoding.UTF8.GetBytes("key1"));
            var rootAfterDelete1 = trie.Root.GetType().Name;
            var innerAfterDel1 = (trie.Root as ExtendedNode)?.InnerNode?.GetType().Name ?? "N/A";
            var branchChildrenAfterDel1 = GetBranchChildCount((trie.Root as ExtendedNode)?.InnerNode as BranchNode);
            var hash1 = trie.Root.GetHash().ToHex();

            trie.Delete(Encoding.UTF8.GetBytes("key2"));
            var rootAfterDelete2 = trie.Root.GetType().Name;
            var innerAfterDel2 = (trie.Root as ExtendedNode)?.InnerNode?.GetType().Name ?? "N/A";

            Assert.True(trie.Root is EmptyNode,
                $"Root should be EmptyNode but is {rootAfterDelete2}. " +
                $"After Put: {rootAfterPut} (inner={innerAfterPut}, children={branchChildrenAfterPut}), " +
                $"After Delete1: {rootAfterDelete1} (inner={innerAfterDel1}, children={branchChildrenAfterDel1}, hash={hash1}), " +
                $"After Delete2: {rootAfterDelete2} (inner={innerAfterDel2})");
        }

        [Fact]
        public void DeleteSingleKeyLeafOnly()
        {
            var trie = new PatriciaTrie();

            trie.Put(Encoding.UTF8.GetBytes("abc"), Encoding.UTF8.GetBytes("value1"));
            var rootAfterPut = trie.Root.GetType().Name;

            trie.Delete(Encoding.UTF8.GetBytes("abc"));
            var rootAfterDelete = trie.Root.GetType().Name;

            Assert.True(trie.Root is EmptyNode,
                $"Root should be EmptyNode but is {rootAfterDelete}. After Put: {rootAfterPut}");
        }

        [Fact]
        public void DeleteFromBranchDirectly()
        {
            var trie = new PatriciaTrie();

            trie.Put(new byte[] { 0x11 }, Encoding.UTF8.GetBytes("value1"));
            trie.Put(new byte[] { 0x12 }, Encoding.UTF8.GetBytes("value2"));

            var rootAfterPut = trie.Root.GetType().Name;
            var extNode = trie.Root as ExtendedNode;
            var innerAfterPut = extNode?.InnerNode?.GetType().Name ?? "N/A";
            var branchAfterPut = extNode?.InnerNode as BranchNode;
            var childrenAfterPut = GetBranchChildCount(branchAfterPut);

            trie.Delete(new byte[] { 0x11 });

            var rootAfterDelete1 = trie.Root.GetType().Name;
            var extNodeAfter = trie.Root as ExtendedNode;
            var innerAfterDel = extNodeAfter?.InnerNode?.GetType().Name ?? "N/A";
            var branchAfterDel = extNodeAfter?.InnerNode as BranchNode;
            var childrenAfterDel = GetBranchChildCount(branchAfterDel);

            Assert.True(trie.Root is LeafNode,
                $"Root should be LeafNode after deleting one of two branch children, but is {rootAfterDelete1}. " +
                $"After Put: {rootAfterPut} (inner={innerAfterPut}, children={childrenAfterPut}). " +
                $"After Del: {rootAfterDelete1} (inner={innerAfterDel}, children={childrenAfterDel})");
        }

        private string GetBranchChildCount(BranchNode branch)
        {
            if (branch == null) return "N/A";
            int count = 0;
            string indices = "";
            for (int i = 0; i < 16; i++)
            {
                if (branch.Children[i] != null && !(branch.Children[i] is EmptyNode))
                {
                    count++;
                    indices += i.ToString("X") + ",";
                }
            }
            return $"{count} ({indices.TrimEnd(',')})";
        }

        private byte[] ConvertKeyToBytes(string key)
        {
            if (key.StartsWith("0x") && key.Length > 2)
            {
                return key.Substring(2).HexToByteArray();
            }
            return Encoding.UTF8.GetBytes(key);
        }

        private byte[] ConvertValueToBytes(string value)
        {
            if (value.StartsWith("0x") && value.Length > 2)
            {
                return value.Substring(2).HexToByteArray();
            }
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
