using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class EventEmitterScopedMatchingTests
    {
        private const string TokenContract = "0x1111111111111111111111111111111111111111";
        private const string OtherContract = "0x4444444444444444444444444444444444444444";
        private const string Sender = "0x2222222222222222222222222222222222222222";
        private const string Receiver = "0x3333333333333333333333333333333333333333";

        private static string AddressTopic(string address) =>
            "0x" + address.HexToByteArray().PadTo32Bytes().ToHex();

        private static FilterLog TransferLogFrom(string emitter, BigInteger value = default)
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
                Data = "0x" + (value == default ? 1000 : value)
                    .ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes().ToHex()
            };
        }

        [Fact]
        public void Given_ALogFromAnotherAddress_When_MatchedAgainstAnAddressScopedEvent_Then_ItIsNotAMatch()
        {
            Assert.False(TransferLogFrom(OtherContract).IsLogForEventEmittedBy<TransferEventDTO>(TokenContract));
        }

        [Fact]
        public void Given_ALogFromTheEventsOwnAddress_When_MatchedAgainstAnAddressScopedEvent_Then_ItIsAMatch()
        {
            Assert.True(TransferLogFrom(TokenContract).IsLogForEventEmittedBy<TransferEventDTO>(TokenContract));
        }

        [Fact]
        public void Given_ALogFromTheSystemAddress_When_MatchedAgainstATokenScopedEvent_Then_ItIsNotAMatch()
        {
            Assert.False(TransferLogFrom(AddressUtil.SYSTEM_ADDRESS)
                .IsLogForEventEmittedBy<TransferEventDTO>(TokenContract));
        }

        [Fact]
        public void Given_NoContractAddress_When_MatchedAgainstAnyEmitter_Then_ItIsAMatch()
        {
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedBy<TransferEventDTO>(null));
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedBy<TransferEventDTO>(string.Empty));
        }

        [Fact]
        public void Given_TheExistingNoAddressOverload_When_ALogFromAnyEmitterIsMatched_Then_ItStillMatchesAsBefore()
        {
            Assert.True(TransferLogFrom(TokenContract).IsLogForEvent<TransferEventDTO>());
            Assert.True(TransferLogFrom(OtherContract).IsLogForEvent<TransferEventDTO>());
            Assert.True(TransferLogFrom(AddressUtil.SYSTEM_ADDRESS).IsLogForEvent<TransferEventDTO>());
        }

        [Fact]
        public void Given_ALogFromOneOfSeveralAddresses_When_MatchedAgainstThatSet_Then_ItIsAMatch()
        {
            Assert.True(TransferLogFrom(OtherContract)
                .IsLogForEventEmittedByAny<TransferEventDTO>(new[] { TokenContract, OtherContract }));
        }

        [Fact]
        public void Given_ALogFromOutsideTheAddressSet_When_MatchedAgainstThatSet_Then_ItIsNotAMatch()
        {
            Assert.False(TransferLogFrom(AddressUtil.SYSTEM_ADDRESS)
                .IsLogForEventEmittedByAny<TransferEventDTO>(new[] { TokenContract, OtherContract }));
        }

        [Fact]
        public void Given_AnEmptyAddressSet_When_MatchedAgainstAnyEmitter_Then_ItIsAMatch()
        {
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedByAny<TransferEventDTO>(new string[0]));
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedByAny<TransferEventDTO>((string[])null));
        }

        [Fact]
        public void Given_ALogWithADifferentTopic_When_MatchedAgainstTheRightAddress_Then_ItIsNotAMatch()
        {
            var wrongTopic = TransferLogFrom(TokenContract);
            wrongTopic.Topics[0] = "0x" + new string('a', 64);

            Assert.False(wrongTopic.IsLogForEventEmittedBy<TransferEventDTO>(TokenContract));
        }

        [Fact]
        public void Given_ALogFromAnotherAddress_When_Decoded_Then_ItIsNull()
        {
            Assert.Null(TransferLogFrom(OtherContract).DecodeEventEmittedBy<TransferEventDTO>(TokenContract));
        }

        [Fact]
        public void Given_ALogFromTheRightAddress_When_Decoded_Then_ItCarriesTheValue()
        {
            var decoded = TransferLogFrom(TokenContract, 1234).DecodeEventEmittedBy<TransferEventDTO>(TokenContract);

            Assert.NotNull(decoded);
            Assert.Equal(1234, decoded.Event.Value);
        }

        [Fact]
        public void Given_LogsFromSeveralEmitters_When_DecodedForOne_Then_OnlyThatEmittersLogsAreReturned()
        {
            var logs = new[]
            {
                TransferLogFrom(TokenContract, 1),
                TransferLogFrom(OtherContract, 2),
                TransferLogFrom(AddressUtil.SYSTEM_ADDRESS, 3)
            };

            var decoded = logs.DecodeAllEventsEmittedBy<TransferEventDTO>(TokenContract);

            Assert.Single(decoded);
            Assert.Equal(1, decoded[0].Event.Value);
        }

        [Fact]
        public void Given_LogsFromSeveralEmitters_When_DecodedForASet_Then_EveryEmitterInTheSetIsReturned()
        {
            var logs = new[]
            {
                TransferLogFrom(TokenContract, 1),
                TransferLogFrom(OtherContract, 2),
                TransferLogFrom(AddressUtil.SYSTEM_ADDRESS, 3)
            };

            var decoded = logs.DecodeAllEventsEmittedByAny<TransferEventDTO>(new[] { TokenContract, OtherContract });

            Assert.Equal(2, decoded.Count);
            Assert.Contains(decoded, d => d.Event.Value == 1);
            Assert.Contains(decoded, d => d.Event.Value == 2);
        }

        [Fact]
        public void Given_LogsFromSeveralEmitters_When_DecodedWithNoAddress_Then_EveryLogIsReturned()
        {
            var logs = new[]
            {
                TransferLogFrom(TokenContract, 1),
                TransferLogFrom(OtherContract, 2),
                TransferLogFrom(AddressUtil.SYSTEM_ADDRESS, 3)
            };

            Assert.Equal(3, logs.DecodeAllEventsEmittedByAny<TransferEventDTO>((string[])null).Count);
            Assert.Equal(3, logs.DecodeAllEvents<TransferEventDTO>().Count);
        }

        [Fact]
        public void Given_AnEventScopedToAContract_When_ALogFromAnotherContractIsMatched_Then_ItIsNotAMatch()
        {
            var scoped = new EventBase(null, TokenContract, ABITypedRegistry.GetEvent<TransferEventDTO>());

            Assert.False(scoped.IsLogForEventEmittedByThisContract(TransferLogFrom(OtherContract)));
            Assert.True(scoped.IsLogForEventEmittedByThisContract(TransferLogFrom(TokenContract)));
        }

        [Fact]
        public void Given_AnEventWithNoContractAddress_When_ALogFromAnyContractIsMatched_Then_ItIsAMatch()
        {
            var unscoped = new EventBase(null, null, ABITypedRegistry.GetEvent<TransferEventDTO>());

            Assert.True(unscoped.IsLogForEventEmittedByThisContract(TransferLogFrom(OtherContract)));
        }

        [Fact]
        public void Given_NoContractAddress_When_AllLogsAreDecodedThroughTheSingleAddressForm_Then_EveryLogIsReturned()
        {
            var logs = new[] { TransferLogFrom(TokenContract, 1), TransferLogFrom(OtherContract, 2) };

            Assert.Equal(2, logs.DecodeAllEventsEmittedBy<TransferEventDTO>(null).Count);
            Assert.Equal(2, logs.DecodeAllEventsEmittedBy<TransferEventDTO>(string.Empty).Count);
        }

        [Fact]
        public void Given_AnAddressNethereumTreatsAsEmpty_When_Matched_Then_ItMeansAnyEmitter()
        {
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedBy<TransferEventDTO>("   "));
            Assert.True(TransferLogFrom(OtherContract).IsLogForEventEmittedBy<TransferEventDTO>("0x0"));
        }
    }
}
