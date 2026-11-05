using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class ZiskAdapterBindingTests : IClassFixture<ZiskAnchorBoundFixture>
    {
        private readonly ZiskAnchorBoundFixture _fixture;

        public ZiskAdapterBindingTests(ZiskAnchorBoundFixture fixture)
        {
            _fixture = fixture;
        }

        private static byte[] Recommit(byte[] publicValues, List<BigInteger> anchorInputs)
        {
            var encoded = anchorInputs.SelectMany(w =>
            {
                var bytes = w.ToByteArray(isUnsigned: true, isBigEndian: true);
                return new byte[32 - bytes.Length].Concat(bytes);
            }).ToArray();
            var commitment = new Sha3Keccack().CalculateHash(encoded);
            var result = (byte[])publicValues.Clone();
            for (int i = 0; i < 8; i++)
            {
                var offset = i * 8;
                result[offset] = commitment[i * 4 + 3];
                result[offset + 1] = commitment[i * 4 + 2];
                result[offset + 2] = commitment[i * 4 + 1];
                result[offset + 3] = commitment[i * 4];
            }
            return result;
        }

        private Task<bool> Verify(byte[] proofBytes, byte[] publicValues, List<BigInteger> anchorInputs) =>
            _fixture.Adapter.VerifyQueryAsync(ZiskAnchorBoundData.AdapterProof(proofBytes, publicValues), anchorInputs);

        private async Task AssertReverts(Func<Task> action)
        {
            await Assert.ThrowsAnyAsync<Exception>(action);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesShorterThanTheSlotsTheAdapterReads_When_TheAdapterVerifiesThem_Then_ItReverts()
        {
            var data = ZiskAnchorBoundData.Load();
            var truncated = data.Proof.PublicValues.Take(data.Proof.PublicValues.Length - 8).ToArray();

            await AssertReverts(() => Verify(data.Proof.ProofBytes, truncated, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesWithATrailingByte_When_TheAdapterVerifiesThem_Then_ItReverts()
        {
            var data = ZiskAnchorBoundData.Load();
            var padded = data.Proof.PublicValues.Concat(new byte[] { 0 }).ToArray();

            await AssertReverts(() => Verify(data.Proof.ProofBytes, padded, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesShorterThanTheCommitment_When_TheAdapterVerifiesThem_Then_ItReverts()
        {
            var data = ZiskAnchorBoundData.Load();

            await AssertReverts(() => Verify(data.Proof.ProofBytes, data.Proof.PublicValues.Take(40).ToArray(), data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnchorInputsOfTheWrongLength_When_TheAdapterVerifiesThem_Then_ItReverts()
        {
            var data = ZiskAnchorBoundData.Load();
            var six = data.AnchorInputs.Take(6).ToList();

            await AssertReverts(() => Verify(data.Proof.ProofBytes, Recommit(data.Proof.PublicValues, six), six));
        }

        public static IEnumerable<object[]> ClaimedBlocks()
        {
            yield return new object[] { 3, 2 };
            yield return new object[] { 4, 2 };
            yield return new object[] { 5, 1 };
            yield return new object[] { 6, 1 };
            yield return new object[] { 7, 1 };
            yield return new object[] { 8, 1 };
        }

        [Theory]
        [MemberData(nameof(ClaimedBlocks))]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnchorInputsCommittedForADifferentBlockThanTheOneExecuted_When_TheAdapterVerifiesThem_Then_ItReverts(int index, int delta)
        {
            var data = ZiskAnchorBoundData.Load();
            var claimed = new List<BigInteger>(data.AnchorInputs);
            claimed[index] += delta;
            if (index == 3) claimed[4] = claimed[3];
            var publicValues = Recommit(data.Proof.PublicValues, claimed);

            await AssertReverts(() => Verify(data.Proof.ProofBytes, publicValues, claimed));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnchorInputsThatClaimARangeOfBlocks_When_TheAdapterVerifiesThem_Then_ItReverts()
        {
            var data = ZiskAnchorBoundData.Load();
            var claimed = new List<BigInteger>(data.AnchorInputs);
            claimed[4] += 1;
            var publicValues = Recommit(data.Proof.PublicValues, claimed);

            await AssertReverts(() => Verify(data.Proof.ProofBytes, publicValues, claimed));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnchorInputsRecommittedWithoutChanges_When_TheAdapterVerifiesThem_Then_TheOriginalProofStillVerifies()
        {
            var data = ZiskAnchorBoundData.Load();
            var publicValues = Recommit(data.Proof.PublicValues, data.AnchorInputs);

            Assert.Equal(data.Proof.PublicValues, publicValues);
            Assert.True(await Verify(data.Proof.ProofBytes, publicValues, data.AnchorInputs));
        }
    }
}
