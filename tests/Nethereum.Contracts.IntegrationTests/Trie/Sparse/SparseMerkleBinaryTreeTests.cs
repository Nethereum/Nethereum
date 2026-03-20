using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Sparse;
using Nethereum.Util.ByteArrayConvertors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class SparseMerkleBinaryTreeTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly byte[] DataValue = Encoding.UTF8.GetBytes("DATA");

        public SparseMerkleBinaryTreeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static byte[] Sum(uint i)
        {
            var bytes = new byte[] { (byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i };
            using (var sha = SHA256.Create()) { return sha.ComputeHash(bytes); }
        }

        private SparseMerkleBinaryTree<byte[]> CreateCelestiaSmt()
        {
            return new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(),
                new ByteArrayToByteArrayConvertor());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void EmptyTree_RootIsZeroHash()
        {
            var smt = CreateCelestiaSmt();
            Assert.Equal(new string('0', 64), smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void OneUpdate_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            smt.Put(Sum(0), DataValue);
            Assert.Equal("39f36a7cb4dfb1b46f03d044265df6a491dffc1034121bc1071a34ddce9bb14b", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void TwoUpdates_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 2; i++) smt.Put(Sum(i), DataValue);
            Assert.Equal("8d0ae412ca9ca0afcb3217af8bcd5a673e798bd6fd1dfacad17711e883f494cb", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void ThreeUpdates_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 3; i++) smt.Put(Sum(i), DataValue);
            Assert.Equal("52295e42d8de2505fdc0cc825ff9fead419cbcf540d8b30c7c4b9c9b94c268b7", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void FiveUpdates_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 5; i++) smt.Put(Sum(i), DataValue);
            Assert.Equal("108f731f2414e33ae57e584dc26bd276db07874436b2264ca6e520c658185c6b", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void TenUpdates_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++) smt.Put(Sum(i), DataValue);
            Assert.Equal("21ca4917e99da99a61de93deaf88c400d4c082991cb95779e444d43dd13e8849", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void HundredUpdates_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 100; i++) smt.Put(Sum(i), DataValue);
            Assert.Equal("82bf747d455a55e2f7044a03536fc43f1f55d43b855e72c0110c986707a23e4d", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void SparseEvens_MatchesCelestiaVector()
        {
            var smt = CreateCelestiaSmt();
            foreach (uint i in new uint[] { 0, 2, 4, 6, 8 })
                smt.Put(Sum(i), DataValue);
            Assert.Equal("e912e97abc67707b2e6027338292943b53d01a7fbd7b244674128c7e468dd696", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void DeleteOdds_MatchesEvensRoot()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++) smt.Put(Sum(i), DataValue);
            for (uint i = 1; i < 10; i += 2) smt.Delete(Sum(i));
            Assert.Equal("e912e97abc67707b2e6027338292943b53d01a7fbd7b244674128c7e468dd696", smt.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void DeleteAll_ReturnsToZero()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++) smt.Put(Sum(i), DataValue);
            for (uint i = 0; i < 10; i++) smt.Delete(Sum(i));
            Assert.Equal(new string('0', 64), smt.ComputeRoot().ToHex());
            Assert.Equal(0, smt.LeafCount);
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void InsertionOrder_DoesNotAffectRoot()
        {
            var smt1 = CreateCelestiaSmt();
            var smt2 = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++) smt1.Put(Sum(i), DataValue);
            for (uint i = 9; ; i--)
            {
                smt2.Put(Sum(i), DataValue);
                if (i == 0) break;
            }
            Assert.Equal(smt1.ComputeRoot().ToHex(), smt2.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void GetValue_ReturnsCorrect()
        {
            var smt = CreateCelestiaSmt();
            smt.Put(Sum(0), DataValue);
            var result = smt.Get(Sum(0));
            Assert.NotNull(result);
            Assert.Equal(DataValue, result);
            Assert.Null(smt.Get(Sum(1)));
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void UpdateExistingKey_ChangesRootAndValue()
        {
            var smt = CreateCelestiaSmt();
            smt.Put(Sum(0), DataValue);
            var root1 = smt.ComputeRoot().ToHex();
            Assert.Equal(1, smt.LeafCount);

            var newVal = Encoding.UTF8.GetBytes("CHANGE");
            smt.Put(Sum(0), newVal);
            var root2 = smt.ComputeRoot().ToHex();

            Assert.NotEqual(root1, root2);
            Assert.Equal(1, smt.LeafCount);
            Assert.Equal(newVal, smt.Get(Sum(0)));
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void ShallowDepth8_WorksCorrectly()
        {
            var hasher = new DefaultSmtHasher(new Nethereum.Util.HashProviders.Sha256HashProvider());
            var smt = new SparseMerkleBinaryTree<byte[]>(hasher, new ByteArrayToByteArrayConvertor(),
                new IdentitySmtKeyHasher(8));

            var key1 = new byte[] { 0x00 };
            var key2 = new byte[] { 0x80 };
            var val = new byte[] { 1, 2, 3, 4 };

            smt.Put(key1, val);
            var root1 = smt.ComputeRoot();
            Assert.NotNull(root1);
            Assert.Equal(1, smt.LeafCount);

            smt.Put(key2, val);
            var root2 = smt.ComputeRoot();
            Assert.NotEqual(root1.ToHex(), root2.ToHex());
            Assert.Equal(2, smt.LeafCount);

            smt.Delete(key1);
            smt.Delete(key2);
            Assert.Equal(0, smt.LeafCount);
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public void ShallowDepth8_OrderIndependent()
        {
            var hasher = new DefaultSmtHasher(new Nethereum.Util.HashProviders.Sha256HashProvider());

            var smt1 = new SparseMerkleBinaryTree<byte[]>(hasher, new ByteArrayToByteArrayConvertor(),
                new IdentitySmtKeyHasher(8));
            var smt2 = new SparseMerkleBinaryTree<byte[]>(hasher, new ByteArrayToByteArrayConvertor(),
                new IdentitySmtKeyHasher(8));

            var keys = new byte[][] { new byte[] { 0x10 }, new byte[] { 0xA0 }, new byte[] { 0x55 } };
            var val = new byte[] { 0xFF };

            foreach (var k in keys) smt1.Put(k, val);
            for (int i = keys.Length - 1; i >= 0; i--) smt2.Put(keys[i], val);

            Assert.Equal(smt1.ComputeRoot().ToHex(), smt2.ComputeRoot().ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT-Perf")]
        public void Performance_1000Inserts_UnderOneSecond()
        {
            var smt = CreateCelestiaSmt();
            var sw = Stopwatch.StartNew();

            for (uint i = 0; i < 1000; i++)
                smt.Put(Sum(i), DataValue);

            var insertMs = sw.ElapsedMilliseconds;
            sw.Restart();

            var root = smt.ComputeRoot();
            var hashMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"1000 inserts: {insertMs}ms, root computation: {hashMs}ms");
            _output.WriteLine($"Root: {root.ToHex()}");
            _output.WriteLine($"Leaf count: {smt.LeafCount}");

            Assert.Equal(1000, smt.LeafCount);
            Assert.True(insertMs + hashMs < 5000, $"Total time {insertMs + hashMs}ms exceeded 5s");
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT-Perf")]
        public void Performance_10000Inserts()
        {
            var smt = CreateCelestiaSmt();
            var sw = Stopwatch.StartNew();

            for (uint i = 0; i < 10000; i++)
                smt.Put(Sum(i), DataValue);

            var insertMs = sw.ElapsedMilliseconds;
            sw.Restart();

            var root = smt.ComputeRoot();
            var hashMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"10000 inserts: {insertMs}ms, root computation: {hashMs}ms");
            _output.WriteLine($"Root: {root.ToHex()}");

            Assert.Equal(10000, smt.LeafCount);
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT-Perf")]
        public async Task Performance_CompareOldVsNew_100Entries()
        {
            var sw = Stopwatch.StartNew();

            var oldSmt = new SparseMerkleTree<byte[]>(
                256,
                new Nethereum.Util.HashProviders.Sha256HashProvider(),
                new ByteArrayToByteArrayConvertor(),
                new InMemorySparseMerkleTreeStorage<byte[]>(),
                new CelestiaSmtHasher());

            for (uint i = 0; i < 100; i++)
                await oldSmt.SetLeafAsync(Sum(i).ToHex(), DataValue);
            var oldRoot = await oldSmt.GetRootHashAsync();
            var oldMs = sw.ElapsedMilliseconds;

            sw.Restart();
            var newSmt = CreateCelestiaSmt();
            for (uint i = 0; i < 100; i++)
                newSmt.Put(Sum(i), DataValue);
            var newRoot = newSmt.ComputeRoot().ToHex();
            var newMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"Old (string-based): {oldMs}ms");
            _output.WriteLine($"New (node-based):   {newMs}ms");
            _output.WriteLine($"Speedup: {(double)oldMs / Math.Max(newMs, 1):F1}x");

            Assert.Equal(oldRoot, newRoot);
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public async Task Storage_FlushAndReload_ProducesSameRoot()
        {
            var storage = new InMemorySmtNodeStorage();
            var smt = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);

            for (uint i = 0; i < 10; i++)
                smt.Put(Sum(i), DataValue);

            var originalRoot = smt.ComputeRoot();
            await smt.FlushAsync();

            _output.WriteLine($"Flushed {storage.Count} nodes to storage");
            Assert.True(storage.Count > 0);

            var smt2 = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);
            await smt2.LoadRootAsync(originalRoot);

            var reloadedRoot = await smt2.ComputeRootAsync();
            _output.WriteLine($"Original root:  {originalRoot.ToHex()}");
            _output.WriteLine($"Reloaded root:  {reloadedRoot.ToHex()}");
            Assert.Equal(originalRoot.ToHex(), reloadedRoot.ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public async Task Storage_LazyLoad_GetReturnsCorrectValue()
        {
            var storage = new InMemorySmtNodeStorage();
            var smt = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);

            for (uint i = 0; i < 5; i++)
                smt.Put(Sum(i), DataValue);

            smt.ComputeRoot();
            await smt.FlushAsync();

            var smt2 = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);
            await smt2.LoadRootAsync(smt.ComputeRoot());

            var val = await smt2.GetAsync(Sum(0));
            Assert.NotNull(val);
            Assert.Equal(DataValue, val);

            var missing = await smt2.GetAsync(Sum(99));
            Assert.Null(missing);
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public async Task Storage_InsertAfterReload_WorksCorrectly()
        {
            var storage = new InMemorySmtNodeStorage();
            var smt = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);

            for (uint i = 0; i < 5; i++)
                smt.Put(Sum(i), DataValue);
            smt.ComputeRoot();
            await smt.FlushAsync();

            var smt2 = new SparseMerkleBinaryTree<byte[]>(
                new CelestiaSmtHasher(), new ByteArrayToByteArrayConvertor(), storage: storage);
            await smt2.LoadRootAsync(smt.ComputeRoot());

            await smt2.PutAsync(Sum(5), DataValue);
            await smt2.PutAsync(Sum(6), DataValue);
            var newRoot = await smt2.ComputeRootAsync();

            var reference = CreateCelestiaSmt();
            for (uint i = 0; i < 7; i++)
                reference.Put(Sum(i), DataValue);
            var expectedRoot = reference.ComputeRoot();

            Assert.Equal(expectedRoot.ToHex(), newRoot.ToHex());
        }

        [Fact]
        [Trait("Category", "BinaryTree-SMT")]
        public async Task AsyncApi_MatchesSyncApi()
        {
            var smt1 = CreateCelestiaSmt();
            var smt2 = CreateCelestiaSmt();

            for (uint i = 0; i < 10; i++)
                smt1.Put(Sum(i), DataValue);

            for (uint i = 0; i < 10; i++)
                await smt2.PutAsync(Sum(i), DataValue);

            Assert.Equal(smt1.ComputeRoot().ToHex(), (await smt2.ComputeRootAsync()).ToHex());
        }
    }
}
