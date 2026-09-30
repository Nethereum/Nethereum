using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class TransferLogFilterTests
    {
        private const string TokenContract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string Receiver = "0x2222222222222222222222222222222222222222";

        private static readonly string TransferSig =
            (string)Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static string PadAddress(string address) =>
            "0x000000000000000000000000" + address.Substring(2).ToLowerInvariant();

        private static FilterLog TransferLog(string emitter) => new FilterLog
        {
            Address = emitter,
            Topics = new object[] { TransferSig, PadAddress(Sender), PadAddress(Receiver) },
            Data = "0x" + ((BigInteger)1000).ToString("x64")
        };

        private static TransactionLog StoredTransferLog(string emitter) => new TransactionLog
        {
            TransactionHash = "0xabc123",
            LogIndex = 0,
            Address = emitter,
            EventHash = TransferSig,
            IndexVal1 = PadAddress(Sender),
            IndexVal2 = PadAddress(Receiver),
            Data = "0x" + ((BigInteger)1000).ToString("x64"),
            BlockNumber = 100,
            BlockHash = "0xblockhash"
        };

        private static Task<int> RebuildAsync(InMemoryTokenTransferLogRepository repository,
            TransferLogFilter filter) =>
            TokenDenormalizerProcessingService.ProcessBatchAsync(
                new ITransactionLogView[]
                {
                    StoredTransferLog(AddressUtil.SYSTEM_ADDRESS),
                    StoredTransferLog(TokenContract)
                },
                repository, filter);

        [Fact]
        public void Given_TheDefault_When_AnEthTransferLogArrives_Then_ItIsRecorded()
        {
            Assert.True(TransferLogFilter.Everything.Matches(TransferLog(AddressUtil.SYSTEM_ADDRESS)));
        }

        [Fact]
        public void Given_TokensOnly_When_AnEthTransferLogArrives_Then_ItIsNotRecorded()
        {
            Assert.False(TransferLogFilter.NativeTransfers.Negated()
                .Matches(TransferLog(AddressUtil.SYSTEM_ADDRESS)));
        }

        [Fact]
        public void Given_TokensOnly_When_ATokenTransferLogArrives_Then_ItIsStillRecorded()
        {
            Assert.True(TransferLogFilter.NativeTransfers.Negated().Matches(TransferLog(TokenContract)));
        }

        [Fact]
        public void Given_TheDefault_When_ATokenTransferLogArrives_Then_ItIsStillRecorded()
        {
            Assert.True(TransferLogFilter.Everything.Matches(TransferLog(TokenContract)));
        }

        [Fact]
        public void Given_NativeTransfers_When_Negated_Then_ItMatchesExactlyWhatTheOriginalDidNot()
        {
            var native = TransferLogFilter.NativeTransfers;
            var negated = native.Negated();

            foreach (var emitter in new[] { AddressUtil.SYSTEM_ADDRESS, TokenContract, Sender })
            {
                var log = TransferLog(emitter);
                Assert.NotEqual(native.Matches(log), negated.Matches(log));
            }
        }

        [Fact]
        public async Task Given_TokensOnly_When_TheDenormaliserRebuilds_Then_ItDoesNotReintroduceEther()
        {
            var repository = new InMemoryTokenTransferLogRepository();

            var written = await RebuildAsync(repository, TransferLogFilter.NativeTransfers.Negated());

            Assert.Equal(1, written);
            Assert.Single(repository.Records);
            Assert.False(repository.Records[0].IsNativeTransfer());
        }

        [Fact]
        public async Task Given_TheDefault_When_TheDenormaliserRebuilds_Then_BothKindsAreWritten()
        {
            var repository = new InMemoryTokenTransferLogRepository();

            var written = await RebuildAsync(repository, TransferLogFilter.Everything);

            Assert.Equal(2, written);
        }

        [Fact]
        public async Task Given_TheSameLog_When_BothWritersUseTheSameFilter_Then_TheyWriteTheSameRows()
        {
            var tokensOnly = TransferLogFilter.NativeTransfers.Negated();

            var live = new InMemoryTokenTransferLogRepository();
            foreach (var emitter in new[] { AddressUtil.SYSTEM_ADDRESS, TokenContract })
            {
                var log = TransferLog(emitter);
                if (tokensOnly.Matches(log))
                    await TokenTransferLogProcessingService.ProcessTransferLogAsync(live, log);
            }

            var rebuilt = new InMemoryTokenTransferLogRepository();
            await RebuildAsync(rebuilt, tokensOnly);

            Assert.Equal(live.Records.Count, rebuilt.Records.Count);
            Assert.Equal(live.Records[0].ContractAddress, rebuilt.Records[0].ContractAddress);
        }
    }
}
