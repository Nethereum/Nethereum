using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.Contracts;
using Nethereum.Util;
using Xunit;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class TokenDenormalizerNativeTransferTests
    {
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string Receiver = "0x2222222222222222222222222222222222222222";
        private const string TokenContract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static readonly string TransferSig =
            (string)Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static string PadAddress(string address) =>
            "0x000000000000000000000000" + address.Substring(2).ToLowerInvariant();

        private static TransactionLog StoredTransferLog(string emitter, BigInteger amount)
        {
            return new TransactionLog
            {
                TransactionHash = "0xabc123",
                LogIndex = 0,
                Address = emitter,
                EventHash = TransferSig,
                IndexVal1 = PadAddress(Sender),
                IndexVal2 = PadAddress(Receiver),
                Data = "0x" + amount.ToString("x64"),
                BlockNumber = 100,
                BlockHash = "0xblockhash"
            };
        }

        private static async Task<List<TokenTransferLog>> DenormaliseAsync(TransactionLog stored)
        {
            var repository = new InMemoryTokenTransferLogRepository();
            await TokenDenormalizerProcessingService.ProcessBatchAsync(
                new ITransactionLogView[] { stored }, repository);
            return repository.Records;
        }

        [Fact]
        public async Task Given_AStoredSystemEmittedTransferLog_When_Denormalised_Then_TheRowReadsAsNative()
        {
            var rows = await DenormaliseAsync(StoredTransferLog(AddressUtil.SYSTEM_ADDRESS, 5000));

            Assert.Single(rows);
            Assert.True(rows[0].IsNativeTransfer());
            Assert.Equal(AddressUtil.SYSTEM_ADDRESS, rows[0].ContractAddress);
        }

        [Fact]
        public async Task Given_AStoredTokenTransferLog_When_Denormalised_Then_TheRowDoesNotReadAsNative()
        {
            var rows = await DenormaliseAsync(StoredTransferLog(TokenContract, 5000));

            Assert.Single(rows);
            Assert.False(rows[0].IsNativeTransfer());
        }

        [Fact]
        public async Task Given_AStoredSystemEmittedTransferLog_When_Denormalised_Then_TheEmitterSurvivesTheRoundTrip()
        {
            var stored = StoredTransferLog(AddressUtil.SYSTEM_ADDRESS, 42);

            Assert.Equal(stored.Address, stored.ToFilterLog().Address);

            var rows = await DenormaliseAsync(stored);
            Assert.Equal("42", rows[0].Amount);
        }
    }
}
