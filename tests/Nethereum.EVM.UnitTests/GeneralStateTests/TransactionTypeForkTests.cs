using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class TransactionTypeForkTests
    {

        [Theory]
        [InlineData(HardforkName.Frontier)]
        [InlineData(HardforkName.Homestead)]
        [InlineData(HardforkName.Berlin)]
        [InlineData(HardforkName.Cancun)]
        [InlineData(HardforkName.Osaka)]
        public void LegacyTx_AcceptedAtEveryFork(HardforkName fork)
            => Assert.True(TransactionTypeFork.IsAcceptedAt(MakeLegacyTx(), fork));


        [Theory]
        [InlineData(HardforkName.Frontier, false)]
        [InlineData(HardforkName.Istanbul, false)]
        [InlineData(HardforkName.MuirGlacier, false)]
        [InlineData(HardforkName.Berlin, true)]
        [InlineData(HardforkName.London, true)]
        [InlineData(HardforkName.Cancun, true)]
        public void Tx2930_Gated_AtBerlin(HardforkName fork, bool expected)
            => Assert.Equal(expected, TransactionTypeFork.IsAcceptedAt(MakeTx2930(), fork));


        [Theory]
        [InlineData(HardforkName.Berlin, false)]
        [InlineData(HardforkName.London, true)]
        [InlineData(HardforkName.Paris, true)]
        [InlineData(HardforkName.Cancun, true)]
        public void Tx1559_Gated_AtLondon(HardforkName fork, bool expected)
            => Assert.Equal(expected, TransactionTypeFork.IsAcceptedAt(MakeTx1559(), fork));


        [Theory]
        [InlineData(HardforkName.London, false)]
        [InlineData(HardforkName.Paris, false)]
        [InlineData(HardforkName.Shanghai, false)]
        [InlineData(HardforkName.Cancun, true)]
        [InlineData(HardforkName.Prague, true)]
        public void Tx4844_Gated_AtCancun(HardforkName fork, bool expected)
            => Assert.Equal(expected, TransactionTypeFork.IsAcceptedAt(MakeTx4844(), fork));


        [Theory]
        [InlineData(HardforkName.Cancun, false)]
        [InlineData(HardforkName.Prague, true)]
        [InlineData(HardforkName.Osaka, true)]
        public void Tx7702_Gated_AtPrague(HardforkName fork, bool expected)
            => Assert.Equal(expected, TransactionTypeFork.IsAcceptedAt(MakeTx7702(), fork));


        private static LegacyTransaction MakeLegacyTx() => new(
            nonce: new byte[] { 0x01 },
            gasPrice: new byte[] { 0x14 },
            gasLimit: new byte[] { 0x52, 0x08 },
            receiveAddress: new byte[20],
            value: new byte[] { },
            data: new byte[0]);

        private static Transaction2930 MakeTx2930() => new(
            chainId: new EvmUInt256(1UL),
            nonce: new EvmUInt256(0UL),
            gasPrice: new EvmUInt256(30UL),
            gasLimit: new EvmUInt256(21000UL),
            receiverAddress: "0x000000000000000000000000000000000000dead",
            amount: new EvmUInt256(0UL),
            data: "0x",
            accessList: new List<Nethereum.Model.AccessListItem>());

        private static Transaction1559 MakeTx1559() => new(
            chainId: new EvmUInt256(1UL),
            nonce: new EvmUInt256(0UL),
            maxPriorityFeePerGas: new EvmUInt256(3UL),
            maxFeePerGas: new EvmUInt256(100UL),
            gasLimit: new EvmUInt256(21000UL),
            receiverAddress: "0x000000000000000000000000000000000000dead",
            amount: new EvmUInt256(0UL),
            data: "0x",
            accessList: new List<Nethereum.Model.AccessListItem>());

        private static Transaction4844 MakeTx4844() => new(
            chainId: new EvmUInt256(1UL),
            nonce: new EvmUInt256(0UL),
            maxPriorityFeePerGas: new EvmUInt256(3UL),
            maxFeePerGas: new EvmUInt256(100UL),
            gasLimit: new EvmUInt256(21000UL),
            receiverAddress: "0x000000000000000000000000000000000000dead",
            amount: new EvmUInt256(0UL),
            data: "0x",
            accessList: new List<Nethereum.Model.AccessListItem>(),
            maxFeePerBlobGas: new EvmUInt256(1000UL),
            blobVersionedHashes: new List<byte[]> { new byte[32] });

        private static Transaction7702 MakeTx7702() => new(
            chainId: new EvmUInt256(1UL),
            nonce: new EvmUInt256(0UL),
            maxPriorityFeePerGas: new EvmUInt256(10UL),
            maxFeePerGas: new EvmUInt256(200UL),
            gasLimit: new EvmUInt256(50000UL),
            receiverAddress: "0x000000000000000000000000000000000000dead",
            amount: new EvmUInt256(0UL),
            data: "0x",
            accessList: new List<Nethereum.Model.AccessListItem>(),
            authorisationList: new List<Authorisation7702Signed>());
    }
}
