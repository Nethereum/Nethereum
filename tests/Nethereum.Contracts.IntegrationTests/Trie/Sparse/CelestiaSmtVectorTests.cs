using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Sparse;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class CelestiaSmtVectorTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly byte[] DataValue = Encoding.UTF8.GetBytes("DATA");

        public CelestiaSmtVectorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static byte[] Sum(uint i)
        {
            var bytes = new byte[4];
            bytes[0] = (byte)(i >> 24);
            bytes[1] = (byte)(i >> 16);
            bytes[2] = (byte)(i >> 8);
            bytes[3] = (byte)i;
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        private SparseMerkleTree<byte[]> CreateCelestiaSmt()
        {
            var storage = new InMemorySparseMerkleTreeStorage<byte[]>();
            var convertor = new ByteArrayToByteArrayConvertor();
            var hasher = new CelestiaSmtHasher();
            return new SparseMerkleTree<byte[]>(256, new Sha256HashProvider(), convertor, storage, hasher);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task EmptyTree_RootIsZeroHash()
        {
            var smt = CreateCelestiaSmt();
            var root = await smt.GetRootHashAsync();
            var expected = new string('0', 64);
            _output.WriteLine($"Empty root: {root}");
            Assert.Equal(expected, root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task OneUpdate_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            var key = Sum(0);
            await smt.SetLeafAsync(key.ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 1 update: {root}");
            Assert.Equal("39f36a7cb4dfb1b46f03d044265df6a491dffc1034121bc1071a34ddce9bb14b", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task TwoUpdates_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 2; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 2 updates: {root}");
            Assert.Equal("8d0ae412ca9ca0afcb3217af8bcd5a673e798bd6fd1dfacad17711e883f494cb", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task ThreeUpdates_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 3; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 3 updates: {root}");
            Assert.Equal("52295e42d8de2505fdc0cc825ff9fead419cbcf540d8b30c7c4b9c9b94c268b7", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task FiveUpdates_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 5; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 5 updates: {root}");
            Assert.Equal("108f731f2414e33ae57e584dc26bd276db07874436b2264ca6e520c658185c6b", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task TenUpdates_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 10 updates: {root}");
            Assert.Equal("21ca4917e99da99a61de93deaf88c400d4c082991cb95779e444d43dd13e8849", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task HundredUpdates_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 100; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after 100 updates: {root}");
            Assert.Equal("82bf747d455a55e2f7044a03536fc43f1f55d43b855e72c0110c986707a23e4d", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task SparseUnion_Evens_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            uint[] indices = { 0, 2, 4, 6, 8 };
            foreach (var i in indices)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root sparse evens: {root}");
            Assert.Equal("e912e97abc67707b2e6027338292943b53d01a7fbd7b244674128c7e468dd696", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task Union_ThreeRanges_MatchesFuelVector()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 5; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);
            for (uint i = 10; i < 15; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);
            for (uint i = 20; i < 25; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root three ranges: {root}");
            Assert.Equal("7e6643325042cfe0fc76626c043b97062af51c7e9fc56665f12b479034bce326", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task DeleteSparse_MatchesEvensRoot()
        {
            var smt = CreateCelestiaSmt();
            for (uint i = 0; i < 10; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            for (uint i = 1; i < 10; i += 2)
                await smt.SetLeafAsync(Sum(i).ToHex(), null);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root after delete odds: {root}");
            Assert.Equal("e912e97abc67707b2e6027338292943b53d01a7fbd7b244674128c7e468dd696", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task InterleavedUpdateDelete_MatchesThreeRangesRoot()
        {
            var smt = CreateCelestiaSmt();

            for (uint i = 0; i < 25; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            for (uint i = 5; i < 10; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), null);
            for (uint i = 15; i < 20; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), null);

            var root = await smt.GetRootHashAsync();
            _output.WriteLine($"Root interleaved: {root}");
            Assert.Equal("7e6643325042cfe0fc76626c043b97062af51c7e9fc56665f12b479034bce326", root);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task InsertionOrder_DoesNotAffectRoot()
        {
            var smt1 = CreateCelestiaSmt();
            var smt2 = CreateCelestiaSmt();

            for (uint i = 0; i < 10; i++)
                await smt1.SetLeafAsync(Sum(i).ToHex(), DataValue);

            for (uint i = 9; i <= 9; i--)
            {
                await smt2.SetLeafAsync(Sum(i).ToHex(), DataValue);
                if (i == 0) break;
            }

            var root1 = await smt1.GetRootHashAsync();
            var root2 = await smt2.GetRootHashAsync();
            Assert.Equal(root1, root2);
        }

        [Fact]
        [Trait("Category", "Celestia-SMT")]
        public async Task DeleteAll_ReturnsToEmptyRoot()
        {
            var smt = CreateCelestiaSmt();

            for (uint i = 0; i < 10; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), DataValue);

            var nonEmptyRoot = await smt.GetRootHashAsync();
            Assert.NotEqual(new string('0', 64), nonEmptyRoot);

            for (uint i = 0; i < 10; i++)
                await smt.SetLeafAsync(Sum(i).ToHex(), null);

            var root = await smt.GetRootHashAsync();
            Assert.Equal(new string('0', 64), root);
        }
    }
}
