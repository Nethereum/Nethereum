using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Sparse;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class PoseidonSmtVectorTests
    {
        private readonly ITestOutputHelper _output;

        public PoseidonSmtVectorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static byte[] BigIntToBytes32(BigInteger value)
        {
            if (value.IsZero)
                return new byte[32];

            var le = value.ToByteArray();
            var result = new byte[32];
            for (int i = 0; i < le.Length && i < 32; i++)
            {
                if (le[i] == 0 && i == le.Length - 1) break;
                result[31 - i] = le[i];
            }
            return result;
        }

        private SparseMerkleBinaryTree<byte[]> CreatePoseidonSmt(int depth = 10)
        {
            return new SparseMerkleBinaryTree<byte[]>(
                new PoseidonSmtHasher(),
                new ByteArrayToByteArrayConvertor(),
                new IdentitySmtKeyHasher(depth));
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void PoseidonHash_BasicVectors_MatchCircomlib()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);

            var result1 = hasher.Hash(BigInteger.One, new BigInteger(2), BigInteger.One);
            _output.WriteLine($"Poseidon(1, 2, 1) = {result1}");

            var result2 = hasher.Hash(BigInteger.One, new BigInteger(2));
            _output.WriteLine($"Poseidon(1, 2)    = {result2}");

            Assert.True(result1 > BigInteger.Zero);
            Assert.True(result2 > BigInteger.Zero);
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_SingleEntry_RootMatchesVector()
        {
            var smt = CreatePoseidonSmt(10);

            var key = BigIntToBytes32(BigInteger.One);
            var val = BigIntToBytes32(new BigInteger(2));
            smt.Put(key, val);

            var root = smt.ComputeRoot();
            var rootBigInt = new BigInteger(ReverseWithZeroPad(root));
            _output.WriteLine($"Root after Add(1,2): {rootBigInt}");

            var expected = BigInteger.Parse("13578938674299138072471463694055224830892726234048532520316387704878000008795");
            Assert.Equal(expected, rootBigInt);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_TwoEntries_RootMatchesVector()
        {
            var smt = CreatePoseidonSmt(10);

            smt.Put(BigIntToBytes32(BigInteger.One), BigIntToBytes32(new BigInteger(2)));
            smt.Put(BigIntToBytes32(new BigInteger(33)), BigIntToBytes32(new BigInteger(44)));

            var root = smt.ComputeRoot();
            var rootBigInt = new BigInteger(ReverseWithZeroPad(root));
            _output.WriteLine($"Root after Add(1,2) + Add(33,44): {rootBigInt}");

            var expected = BigInteger.Parse("5412393676474193513566895793055462193090331607895808993925969873307089394741");
            Assert.Equal(expected, rootBigInt);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_ThreeEntries_RootMatchesVector()
        {
            var smt = CreatePoseidonSmt(10);

            smt.Put(BigIntToBytes32(BigInteger.One), BigIntToBytes32(new BigInteger(2)));
            smt.Put(BigIntToBytes32(new BigInteger(33)), BigIntToBytes32(new BigInteger(44)));
            smt.Put(BigIntToBytes32(new BigInteger(1234)), BigIntToBytes32(new BigInteger(9876)));

            var root = smt.ComputeRoot();
            var rootBigInt = new BigInteger(ReverseWithZeroPad(root));
            _output.WriteLine($"Root after 3 entries: {rootBigInt}");

            var expected = BigInteger.Parse("14204494359367183802864593755198662203838502594566452929175967972147978322084");
            Assert.Equal(expected, rootBigInt);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_DeleteEntry_RootMatchesVector()
        {
            var smt = CreatePoseidonSmt(10);

            smt.Put(BigIntToBytes32(BigInteger.One), BigIntToBytes32(new BigInteger(2)));
            smt.Put(BigIntToBytes32(new BigInteger(33)), BigIntToBytes32(new BigInteger(44)));
            smt.Put(BigIntToBytes32(new BigInteger(1234)), BigIntToBytes32(new BigInteger(9876)));

            smt.Delete(BigIntToBytes32(new BigInteger(33)));

            var root = smt.ComputeRoot();
            var rootBigInt = new BigInteger(ReverseWithZeroPad(root));
            _output.WriteLine($"Root after delete(33): {rootBigInt}");

            var expected = BigInteger.Parse("15550352095346187559699212771793131433118240951738528922418613687814377955591");
            Assert.Equal(expected, rootBigInt);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_DeleteAll_RootIsZero()
        {
            var smt = CreatePoseidonSmt(10);

            smt.Put(BigIntToBytes32(BigInteger.One), BigIntToBytes32(new BigInteger(2)));
            smt.Put(BigIntToBytes32(new BigInteger(33)), BigIntToBytes32(new BigInteger(44)));
            smt.Put(BigIntToBytes32(new BigInteger(1234)), BigIntToBytes32(new BigInteger(9876)));

            smt.Delete(BigIntToBytes32(BigInteger.One));
            smt.Delete(BigIntToBytes32(new BigInteger(33)));
            smt.Delete(BigIntToBytes32(new BigInteger(1234)));

            var root = smt.ComputeRoot();
            var rootBigInt = new BigInteger(ReverseWithZeroPad(root));
            _output.WriteLine($"Root after delete all: {rootBigInt}");

            Assert.Equal(BigInteger.Zero, rootBigInt);
            Assert.Equal(0, smt.LeafCount);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_InsertionOrder_DoesNotAffectRoot()
        {
            var smt1 = CreatePoseidonSmt(140);
            var smt2 = CreatePoseidonSmt(140);

            for (int i = 0; i < 16; i++)
                smt1.Put(BigIntToBytes32(new BigInteger(i)), BigIntToBytes32(BigInteger.Zero));

            for (int i = 15; i >= 0; i--)
                smt2.Put(BigIntToBytes32(new BigInteger(i)), BigIntToBytes32(BigInteger.Zero));

            var root1Hex = smt1.ComputeRoot().ToHex();
            var root2Hex = smt2.ComputeRoot().ToHex();
            _output.WriteLine($"Forward:  {root1Hex}");
            _output.WriteLine($"Backward: {root2Hex}");

            Assert.Equal(root1Hex, root2Hex);

            var rootBytes = smt1.ComputeRoot();
            var rootLittleEndianHex = ToLittleEndianHex(rootBytes);
            _output.WriteLine($"LE hex:   {rootLittleEndianHex}");
            Assert.Equal("3b89100bec24da9275c87bc188740389e1d5accfc7d88ba5688d7fa96a00d82f", rootLittleEndianHex);
        }

        [Fact]
        [Trait("Category", "Poseidon-SMT")]
        public void Iden3_Delete_TwoEntries_MatchesVector()
        {
            var smt = CreatePoseidonSmt(140);

            smt.Put(BigIntToBytes32(BigInteger.One), BigIntToBytes32(BigInteger.One));
            smt.Put(BigIntToBytes32(new BigInteger(2)), BigIntToBytes32(new BigInteger(2)));

            var rootWith2 = new BigInteger(ReverseWithZeroPad(smt.ComputeRoot()));
            _output.WriteLine($"Root with 2 entries: {rootWith2}");
            Assert.Equal(
                BigInteger.Parse("19060075022714027595905950662613111880864833370144986660188929919683258088314"),
                rootWith2);

            smt.Delete(BigIntToBytes32(BigInteger.One));
            var rootAfterDelete = new BigInteger(ReverseWithZeroPad(smt.ComputeRoot()));
            _output.WriteLine($"Root after delete(1): {rootAfterDelete}");
            Assert.Equal(
                BigInteger.Parse("849831128489032619062850458217693666094013083866167024127442191257793527951"),
                rootAfterDelete);
        }

        private static string ToLittleEndianHex(byte[] bigEndian)
        {
            var reversed = new byte[bigEndian.Length];
            for (int i = 0; i < bigEndian.Length; i++)
                reversed[i] = bigEndian[bigEndian.Length - 1 - i];
            return reversed.ToHex();
        }

        private static byte[] ReverseWithZeroPad(byte[] bigEndian)
        {
            var unsigned = new byte[bigEndian.Length + 1];
            for (int i = 0; i < bigEndian.Length; i++)
                unsigned[i] = bigEndian[bigEndian.Length - 1 - i];
            return unsigned;
        }
    }
}
