using System.Collections.Generic;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class SignedTransactionExtensionsTests
    {
        private const string ToAddress = "0x000000000000000000000000000000000000dead";
        private static readonly List<AccessListItem> EmptyAccessList = new List<AccessListItem>();

        [Fact]
        public void Given_LegacyTx_When_GetEffectiveGasPrice_Then_ReturnsGasPrice_IgnoresBaseFee()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: new byte[0]);

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(7UL));

            Assert.Equal(new EvmUInt256(20UL), effective);
        }

        [Fact]
        public void Given_Tx2930_When_GetEffectiveGasPrice_Then_ReturnsGasPrice_IgnoresBaseFee()
        {
            var tx = new Transaction2930(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                gasPrice: new EvmUInt256(30UL),
                gasLimit: new EvmUInt256(21000UL),
                receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: EmptyAccessList);

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(7UL));

            Assert.Equal(new EvmUInt256(30UL), effective);
        }

        [Fact]
        public void Given_Tx1559_PriorityCapped_When_GetEffectiveGasPrice_Then_BaseFeePlusPriorityFee()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL),
                gasLimit: new EvmUInt256(21000UL),
                receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: EmptyAccessList);

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(20UL));

            Assert.Equal(new EvmUInt256(25UL), effective);
        }

        [Fact]
        public void Given_Tx1559_BaseFeeAbsorbsMaxFee_When_GetEffectiveGasPrice_Then_BaseFeePlusReducedTip()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                maxPriorityFeePerGas: new EvmUInt256(50UL),
                maxFeePerGas: new EvmUInt256(30UL),
                gasLimit: new EvmUInt256(21000UL),
                receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: EmptyAccessList);

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(20UL));

            Assert.Equal(new EvmUInt256(30UL), effective);
        }

        [Fact]
        public void Given_Tx4844_When_GetEffectiveGasPrice_Then_BaseFeePlusPriorityFee()
        {
            var tx = new Transaction4844(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                maxPriorityFeePerGas: new EvmUInt256(3UL),
                maxFeePerGas: new EvmUInt256(100UL),
                gasLimit: new EvmUInt256(21000UL),
                receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: EmptyAccessList,
                maxFeePerBlobGas: new EvmUInt256(1000UL),
                blobVersionedHashes: new List<byte[]> { new byte[32] });

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(50UL));

            Assert.Equal(new EvmUInt256(53UL), effective);
        }

        [Fact]
        public void Given_Tx7702_When_GetEffectiveGasPrice_Then_BaseFeePlusPriorityFee()
        {
            var tx = new Transaction7702(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                maxPriorityFeePerGas: new EvmUInt256(10UL),
                maxFeePerGas: new EvmUInt256(200UL),
                gasLimit: new EvmUInt256(50000UL),
                receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: EmptyAccessList,
                authorisationList: new List<Authorisation7702Signed>());

            var effective = tx.GetEffectiveGasPrice(baseFee: new EvmUInt256(100UL));

            Assert.Equal(new EvmUInt256(110UL), effective);
        }
    }
}
