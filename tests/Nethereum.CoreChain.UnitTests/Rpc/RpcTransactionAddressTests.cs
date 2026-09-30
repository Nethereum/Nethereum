using System.Collections.Generic;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class RpcTransactionAddressTests
    {
        private const string ToAddress = "0x000000000000000000000000000000000000dead";
        private static Signature Sig() => new Signature(new byte[32], new byte[32], new byte[] { 0 });

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0x")]
        public void Given_AbsentAddress_When_Normalized_Then_Null(string address)
        {
            Assert.Null(RpcTransactionAddress.Normalize(address));
        }

        [Fact]
        public void Given_ChecksummedAddress_When_Normalized_Then_LowercasePrefixed()
        {
            var checksummed = "0xdAC17F958D2ee523a2206206994597C13D831ec7";
            Assert.Equal("0xdac17f958d2ee523a2206206994597c13d831ec7", RpcTransactionAddress.Normalize(checksummed));
        }

        [Fact]
        public void Given_BareHexAddress_When_Normalized_Then_0xPrefixedLowercase()
        {
            Assert.Equal("0xdac17f958d2ee523a2206206994597c13d831ec7",
                RpcTransactionAddress.Normalize("DAC17F958D2ee523a2206206994597C13D831ec7"));
        }

        [Fact]
        public void Given_ShortAddress_When_Normalized_Then_LeftPaddedTo20Bytes()
        {
            Assert.Equal("0x000000000000000000000000000000000000dead", RpcTransactionAddress.Normalize("dead"));
        }

        [Fact]
        public void Given_Tx4844_When_ResolveReceiver_Then_IsRecipientNotNull()
        {
            var tx = new Transaction4844(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(3UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(),
                maxFeePerBlobGas: new EvmUInt256(1000UL), blobVersionedHashes: new List<byte[]> { new byte[32] }, Sig());

            Assert.Equal(ToAddress, RpcTransactionAddress.ResolveReceiver(tx));
        }

        [Fact]
        public void Given_LegacyCreation_When_ResolveReceiver_Then_IsNull()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[0],
                value: new byte[] { },
                data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);

            Assert.Null(RpcTransactionAddress.ResolveReceiver(tx));
        }

        [Fact]
        public void Given_LegacyWithRecipient_When_ResolveReceiver_Then_IsAddress()
        {
            var recipient = ToAddress.HexToByteArray();
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: recipient,
                value: new byte[] { },
                data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);

            Assert.Equal(ToAddress, RpcTransactionAddress.ResolveReceiver(tx));
        }

        [Fact]
        public void Given_Tx1559Creation_When_ResolveReceiver_Then_IsNull()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: null,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());

            Assert.Null(RpcTransactionAddress.ResolveReceiver(tx));
        }

        [Fact]
        public void Given_Null_When_ResolveReceiver_Then_IsNull()
        {
            Assert.Null(RpcTransactionAddress.ResolveReceiver(null));
        }
    }
}
