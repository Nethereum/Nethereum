using System.Linq;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Contracts.Standards.EthTransfers;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    /// <summary>
    /// EIP-7708 (Amsterdam) makes ETH movement emit
    /// <c>Transfer(address,address,uint256)</c> from the protocol's system address, so
    /// value transfers reach the same filters ERC-20 transfers do. That is the EIP's
    /// intent, and it means an ETH transfer and a token transfer are IDENTICAL in
    /// topic0, in indexed count and in data layout.
    ///
    /// <para>The emitting address is the only discriminator. These tests exist because
    /// every existing "is this an ERC-20 transfer" check in the library answers from the
    /// topic alone, and on an Amsterdam chain that answer is yes for every ETH send.</para>
    /// </summary>
    public class EthTransferLogExtensionsTests
    {
        private const string TokenContract = "0x1111111111111111111111111111111111111111";
        private const string Sender = "0x2222222222222222222222222222222222222222";
        private const string Receiver = "0x3333333333333333333333333333333333333333";

        private static string AddressTopic(string address) =>
            "0x" + address.HexToByteArray().PadTo32Bytes().ToHex();

        /// <summary>A Transfer log as either an ERC-20 token or EIP-7708 would emit it -
        /// the ONLY difference between the two cases is <paramref name="emitter"/>.</summary>
        private static FilterLog TransferLogFrom(string emitter, BigInteger value)
        {
            var signature = ABITypedRegistry.GetEvent<TransferEventDTO>().Sha3Signature;
            return new FilterLog
            {
                Address = emitter,
                Topics = new object[]
                {
                    "0x" + signature,
                    AddressTopic(Sender),
                    AddressTopic(Receiver)
                },
                Data = "0x" + value.ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes().ToHex()
            };
        }

        private static FilterLog EthTransferLog(BigInteger value = default) =>
            TransferLogFrom(AddressUtil.SYSTEM_ADDRESS, value == default ? 1000 : value);

        private static FilterLog TokenTransferLog(BigInteger value = default) =>
            TransferLogFrom(TokenContract, value == default ? 1000 : value);

        [Fact]
        public void Given_ATransferLogFromTheSystemAddress_When_Asked_Then_ItIsAnEthTransfer()
        {
            Assert.True(EthTransferLog().IsEthTransfer());
        }

        [Fact]
        public void Given_ATransferLogFromATokenContract_When_Asked_Then_ItIsNotAnEthTransfer()
        {
            Assert.False(TokenTransferLog().IsEthTransfer());
        }

        [Fact]
        public void Given_ATransferLogFromATokenContract_When_AskedIfTokenTransfer_Then_Yes()
        {
            Assert.True(TokenTransferLog().IsErc20TransferAndNotEthTransfer());
        }

        [Fact]
        public void Given_AnEthTransferLog_When_AskedIfTokenTransfer_Then_No()
        {
            Assert.False(EthTransferLog().IsErc20TransferAndNotEthTransfer());
        }

        /// <summary>
        /// A non-Transfer log from the system address - the pre-transaction system calls
        /// EIP-4788 and EIP-2935 make come from the same address. Emitter alone is not
        /// enough to call something a transfer.
        /// </summary>
        [Fact]
        public void Given_ANonTransferLogFromTheSystemAddress_When_Asked_Then_ItIsNotAnEthTransfer()
        {
            var log = EthTransferLog();
            log.Topics = new object[] { "0x" + new string('a', 64) };

            Assert.False(log.IsEthTransfer());
            Assert.True(log.IsFromEthTransferEmitter());
        }

        [Fact]
        public void Given_AnEthTransferLog_When_Decoded_Then_TheSenderReceiverAndValueAreReadBack()
        {
            var decoded = EthTransferLog(1234).DecodeEthTransfer();

            Assert.NotNull(decoded);
            Assert.True(decoded.Event.From.IsTheSameAddress(Sender));
            Assert.True(decoded.Event.To.IsTheSameAddress(Receiver));
            Assert.Equal(new BigInteger(1234), decoded.Event.Value);
        }

        [Fact]
        public void Given_ATokenTransferLog_When_DecodedAsAnEthTransfer_Then_NothingIsReturned()
        {
            Assert.Null(TokenTransferLog(1234).DecodeEthTransfer());
        }

        [Fact]
        public void Given_OnlyTokenTransfers_When_Classified_Then_TheAnswerMatchesATopicOnlyCheck()
        {
            var log = TokenTransferLog();

            Assert.Equal(log.IsLogForEvent<TransferEventDTO>(),
                         log.IsErc20TransferAndNotEthTransfer());
        }

        [Fact]
        public void Given_ADecodedEthTransfer_When_Asked_Then_ItIsNative()
        {
            Assert.True(EthTransferLog(500).DecodeEthTransfer().IsNativeTransfer());
        }

        [Fact]
        public void Given_ADecodedTokenTransfer_When_Asked_Then_ItIsNotNative()
        {
            Assert.False(TokenTransferLog(500).DecodeEvent<TransferEventDTO>().IsNativeTransfer());
        }

        [Fact]
        public void Given_AMixedResultSet_When_Split_Then_EachSideCarriesOnlyItsOwn()
        {
            var mixed = new[]
            {
                EthTransferLog(1).DecodeEvent<TransferEventDTO>(),
                TokenTransferLog(2).DecodeEvent<TransferEventDTO>(),
                EthTransferLog(3).DecodeEvent<TransferEventDTO>()
            };

            Assert.Equal(2, mixed.NativeTransfers().Count());
            Assert.Single(mixed.TokenTransfers());
            Assert.Equal(3, mixed.NativeTransfers().Count() + mixed.TokenTransfers().Count());
        }


        private const string TransferTopicFromTheEip =
            "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

        private static string[] StringShapeTopics(string emitterSideFrom = Sender) => new[]
        {
            TransferTopicFromTheEip, AddressTopic(emitterSideFrom), AddressTopic(Receiver)
        };

        private static string StringShapeData(BigInteger value) =>
            "0x" + value.ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes().ToHex();

        [Fact]
        public void Given_ALogInTheWalletsStringShape_When_Decoded_Then_ItIsAnEthTransfer()
        {
            var decoded = EthTransferLogExtensions.DecodeEthTransfer(
                AddressUtil.SYSTEM_ADDRESS, StringShapeTopics(), StringShapeData(1500));

            Assert.NotNull(decoded);
            Assert.True(decoded.Event.From.IsTheSameAddress(Sender));
            Assert.True(decoded.Event.To.IsTheSameAddress(Receiver));
            Assert.Equal(new BigInteger(1500), decoded.Event.Value);
        }

        [Fact]
        public void Given_ATokenTransferLogInTheStringShape_When_Decoded_Then_ItIsNull()
        {
            Assert.Null(EthTransferLogExtensions.DecodeEthTransfer(
                TokenContract, StringShapeTopics(), StringShapeData(1500)));
        }

        [Fact]
        public void Given_TheStringShapeAndTheFilterLogShape_When_BothDecoded_Then_TheyAgree()
        {
            var fromFilterLog = EthTransferLog(1500).DecodeEthTransfer();
            var fromStrings = EthTransferLogExtensions.DecodeEthTransfer(
                AddressUtil.SYSTEM_ADDRESS, StringShapeTopics(), StringShapeData(1500));

            Assert.NotNull(fromFilterLog);
            Assert.NotNull(fromStrings);
            Assert.Equal(fromFilterLog.Event.Value, fromStrings.Event.Value);
            Assert.True(fromFilterLog.Event.From.IsTheSameAddress(fromStrings.Event.From));
            Assert.True(fromFilterLog.Event.To.IsTheSameAddress(fromStrings.Event.To));
        }

        [Fact]
        public void Given_AFourTopicLogFromTheEmitter_When_DecodedInTheStringShape_Then_ItIsNull()
        {
            var fourTopics = new[]
            {
                TransferTopicFromTheEip, AddressTopic(Sender), AddressTopic(Receiver), AddressTopic(TokenContract)
            };

            Assert.Null(EthTransferLogExtensions.DecodeEthTransfer(
                AddressUtil.SYSTEM_ADDRESS, fourTopics, StringShapeData(1)));
        }

        [Fact]
        public void Given_NullTopics_When_DecodedInTheStringShape_Then_ItIsNullRatherThanThrowing()
        {
            Assert.Null(EthTransferLogExtensions.DecodeEthTransfer(
                AddressUtil.SYSTEM_ADDRESS, null, StringShapeData(1)));
        }

        [Fact]
        public void Given_TheTopicTheEipDefines_When_ComparedToTheDecodedEvent_Then_TheyAgree()
        {
            var decoded = EthTransferLogExtensions.DecodeEthTransfer(
                AddressUtil.SYSTEM_ADDRESS, StringShapeTopics(), StringShapeData(7));

            Assert.NotNull(decoded);
            Assert.True(TransferTopicFromTheEip.IsTheSameHex(decoded.Log.Topics[0].ToString()));
        }

    }
}
