using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-7685 §Block Header, the commitment this pins:
    /// <code>
    /// def compute_requests_hash(block_requests):
    ///     m = sha256()
    ///     for r in block_requests:
    ///         if len(r) > 1:
    ///             m.update(sha256(r).digest())
    ///     return m.digest()
    /// </code>
    /// </summary>
    public class Eip7685RequestsHashTests
    {
        private static byte[] Sha256Of(byte[] data)
        {
            using (var sha256 = SHA256.Create()) return sha256.ComputeHash(data);
        }

        [Fact]
        public void Given_ABlockWithNoRequests_When_TheHashIsComputed_Then_ItIsSha256OfTheEmptyString()
        {
            var hash = ExecutionRequests.ComputeRequestsHash(new List<byte[]>());

            Assert.Equal(Sha256Of(new byte[0]).ToHex(), hash.ToHex());
        }

        /// <summary>
        /// EIP-7685: <i>"Items with empty <c>request_data</c> are excluded, i.e. the intermediate
        /// list skips <c>requests</c> items which contain only the <c>request_type</c> (1 byte)
        /// and nothing else."</i> An idle predeploy returns nothing, so this is the ordinary case
        /// on almost every block, and dropping the rule changes the hash of all of them.
        /// </summary>
        [Fact]
        public void Given_ARequestListOfOnlyTypeOnlyEntries_When_TheHashIsComputed_Then_ItEqualsTheEmptyBlockHash()
        {
            var typeOnly = new List<byte[]>
            {
                new byte[] { 0x01 }, new byte[] { 0x02 }, new byte[] { 0x03 }, new byte[] { 0x04 }
            };

            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new List<byte[]>()).ToHex(),
                ExecutionRequests.ComputeRequestsHash(typeOnly).ToHex());
        }

        [Fact]
        public void Given_ATypeOnlyEntryBesideARealOne_When_TheHashIsComputed_Then_OnlyTheRealOneContributes()
        {
            var withIdlePredeploys = new List<byte[]>
            {
                new byte[] { 0x01 }, new byte[] { 0x02, 0xaa, 0xbb }, new byte[] { 0x03 }
            };
            var realOneAlone = new List<byte[]> { new byte[] { 0x02, 0xaa, 0xbb } };

            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(realOneAlone).ToHex(),
                ExecutionRequests.ComputeRequestsHash(withIdlePredeploys).ToHex());
        }

        [Fact]
        public void Given_TwoRequests_When_TheHashIsComputed_Then_ItIsTheHashOfTheirDigestsNotOfTheirBytes()
        {
            var first = new byte[] { 0x01, 0x11 };
            var second = new byte[] { 0x02, 0x22 };

            var expected = Sha256Of(Sha256Of(first).Concat(Sha256Of(second)).ToArray());
            var flattened = Sha256Of(first.Concat(second).ToArray());

            var actual = ExecutionRequests.ComputeRequestsHash(new List<byte[]> { first, second });

            Assert.Equal(expected.ToHex(), actual.ToHex());
            Assert.NotEqual(flattened.ToHex(), actual.ToHex());
        }

        [Fact]
        public void Given_TheSameRequestsInADifferentOrder_When_TheHashIsComputed_Then_TheHashDiffers()
        {
            var a = new byte[] { 0x01, 0x11 };
            var b = new byte[] { 0x02, 0x22 };

            Assert.NotEqual(
                ExecutionRequests.ComputeRequestsHash(new List<byte[]> { a, b }).ToHex(),
                ExecutionRequests.ComputeRequestsHash(new List<byte[]> { b, a }).ToHex());
        }

        /// <summary>
        /// EIP-7685 §Block Header: <i>"Within the intermediate list, <c>requests</c> items must be
        /// ordered by <c>request_type</c> ascending."</i> The order decides the commitment, so an
        /// engine that emitted the same requests in another order would commit differently while
        /// every other test agreed with it.
        /// </summary>
        [Fact]
        public void Given_TheRequestTypesTheEngineEmits_When_TheyAreListed_Then_TheyAscendByType()
        {
            var emitted = new List<byte> { ExecutionRequests.DepositRequestType };
            emitted.AddRange(SystemCallContracts
                .RequestContractsFor(HardforkName.Amsterdam)
                .Select(ExecutionRequests.RequestTypeFor));

            Assert.Equal(emitted.OrderBy(type => type).ToList(), emitted);
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }, emitted.ToArray());
        }

        [Fact]
        public void Given_TheRequestTypesAtPrague_When_TheyAreListed_Then_TheyAscendByTypeToo()
        {
            var emitted = new List<byte> { ExecutionRequests.DepositRequestType };
            emitted.AddRange(SystemCallContracts
                .RequestContractsFor(HardforkName.Prague)
                .Select(ExecutionRequests.RequestTypeFor));

            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, emitted.ToArray());
        }

        [Fact]
        public void Given_APredeployAddress_When_ItsRequestTypeIsAsked_Then_ItIsTheOneItsEipAssigns()
        {
            Assert.Equal(0x01, ExecutionRequests.RequestTypeFor(SystemCallContracts.WithdrawalRequests));
            Assert.Equal(0x02, ExecutionRequests.RequestTypeFor(SystemCallContracts.ConsolidationRequests));
            Assert.Equal(0x03, ExecutionRequests.RequestTypeFor(SystemCallContracts.BuilderDeposit));
            Assert.Equal(0x04, ExecutionRequests.RequestTypeFor(SystemCallContracts.BuilderExit));
        }

        [Fact]
        public void Given_AnAddressThatIsNotARequestPredeploy_When_ItsRequestTypeIsAsked_Then_ItIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExecutionRequests.RequestTypeFor(SystemCallContracts.BeaconRoots));
        }

        [Fact]
        public void Given_RequestData_When_ItIsComposed_Then_TheTypeBytePrefixesIt()
        {
            var composed = ExecutionRequests.Compose(0x03, new byte[] { 0xde, 0xad });

            Assert.Equal("0x03dead", composed.ToHex(true));
        }

        [Fact]
        public void Given_NoRequestData_When_ItIsComposed_Then_ItIsTypeOnlyAndCarriesNoData()
        {
            var composed = ExecutionRequests.Compose(0x03, new byte[0]);

            Assert.Equal("0x03", composed.ToHex(true));
            Assert.False(ExecutionRequests.CarriesData(composed));
        }
    }
}
