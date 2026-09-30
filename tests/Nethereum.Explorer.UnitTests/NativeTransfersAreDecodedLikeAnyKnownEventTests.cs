using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Explorer.Services;
using Nethereum.ABI.ABIRepository;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Explorer.UnitTests
{
    public class NativeTransfersAreDecodedLikeAnyKnownEventTests
    {
        private const string TransferTopic =
            "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
        private const string TokenContract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string Receiver = "0x2222222222222222222222222222222222222222";

        private sealed class NoStoredAbis : IAbiStorageService
        {
            public Task<ABIInfo?> GetContractAbiAsync(string contractAddress) =>
                Task.FromResult<ABIInfo?>(null);

            public Task StoreAbiAsync(string contractAddress, string abi, string? name = null,
                AbiSource source = AbiSource.LocalUpload) => Task.CompletedTask;
        }

        private static string PadAddress(string address) =>
            "0x000000000000000000000000" + address.Substring(2);

        private static RawEventLog TransferLog(string emitter) => new RawEventLog
        {
            LogIndex = "0x0",
            Address = emitter,
            EventHash = TransferTopic,
            IndexVal1 = PadAddress(Sender),
            IndexVal2 = PadAddress(Receiver),
            Data = "0x" + new System.Numerics.BigInteger(1000).ToString("x64")
        };

        private static Task<List<DecodedEventLog>> DecodeAsync(string emitter) =>
            new AbiDecodingService(new NoStoredAbis(), NullLogger<AbiDecodingService>.Instance)
                .DecodeEventLogsAsync(emitter, new[] { TransferLog(emitter) });

        [Fact]
        public async Task Given_ALogFromTheEthTransferEmitter_When_Decoded_Then_ItIsDecodedAsATransferWithoutAStoredAbi()
        {
            var decoded = await DecodeAsync(AddressUtil.SYSTEM_ADDRESS);

            Assert.Single(decoded);
            Assert.True(decoded[0].IsDecoded);
            Assert.Equal("Transfer", decoded[0].EventName);
            Assert.Equal(3, decoded[0].Parameters.Count);
        }

        [Fact]
        public async Task Given_TheSameLogFromATokenContract_When_NoAbiIsStored_Then_ItIsNotDecoded()
        {
            var decoded = await DecodeAsync(TokenContract);

            Assert.Single(decoded);
            Assert.False(decoded[0].IsDecoded);
        }
    }
}
