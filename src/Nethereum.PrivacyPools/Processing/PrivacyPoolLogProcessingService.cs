using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.PrivacyPools.PrivacyPoolBase;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif

namespace Nethereum.PrivacyPools.Processing
{
    public class PrivacyPoolLogProcessingService
    {
        protected IBlockchainLogProcessingService _blockchainLogProcessing;
        protected IEthApiContractService _ethApiContractService;

        public string PoolAddress { get; protected set; }

        public PrivacyPoolLogProcessingService(IWeb3 web3, string poolAddress)
        {
            _ethApiContractService = web3.Eth;
            _blockchainLogProcessing = web3.Processing.Logs;
            PoolAddress = poolAddress;
        }

        public PrivacyPoolLogProcessingService(IEthApiContractService ethApiContractService,
            IBlockchainLogProcessingService blockchainLogProcessing, string poolAddress)
        {
            _ethApiContractService = ethApiContractService;
            _blockchainLogProcessing = blockchainLogProcessing;
            PoolAddress = poolAddress;
        }

        public BlockchainProcessor CreateProcessor(
            IPrivacyPoolRepository repository,
            PoseidonMerkleTree stateTree = null,
            IBlockProgressRepository blockProgressRepository = null,
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            ILogger log = null,
#endif
            int numberOfBlocksPerRequest = 100,
            int retryWeight = 50,
            uint minimumNumberOfConfirmations = 0)
        {
            var logProcessorHandler = new ProcessorHandler<FilterLog>(
                action: async (filterLog) =>
                    await ProcessPoolEventAsync(repository, stateTree, filterLog),
                criteria: (filterLog) => filterLog.Removed == false);

            var filterInput = new NewFilterInput()
            {
                Address = new[] { PoolAddress },
                Topics = new object[]
                {
                    new[]
                    {
                        EventExtensions.GetEventABI<DepositedEventDTO>().Sha3Signature,
                        EventExtensions.GetEventABI<WithdrawnEventDTO>().Sha3Signature,
                        EventExtensions.GetEventABI<RagequitEventDTO>().Sha3Signature,
                        EventExtensions.GetEventABI<LeafInsertedEventDTO>().Sha3Signature
                    }
                }
            };

            return _blockchainLogProcessing.CreateProcessor(
                new ProcessorHandler<FilterLog>[] { logProcessorHandler },
                minimumNumberOfConfirmations,
                filterInput,
                blockProgressRepository,
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
                log,
#endif
                numberOfBlocksPerRequest,
                retryWeight);
        }

        private static async Task ProcessPoolEventAsync(
            IPrivacyPoolRepository repository,
            PoseidonMerkleTree stateTree,
            FilterLog filterLog)
        {
            if (filterLog.IsLogForEvent<DepositedEventDTO>())
            {
                var decoded = filterLog.DecodeEvent<DepositedEventDTO>();
                await repository.AddDepositAsync(new PoolDepositEventData
                {
                    Commitment = decoded.Event.Commitment,
                    Label = decoded.Event.Label,
                    Value = decoded.Event.Value,
                    PrecommitmentHash = decoded.Event.PrecommitmentHash,
                    Depositor = decoded.Event.Depositor,
                    BlockNumber = (BigInteger)filterLog.BlockNumber.Value,
                    TransactionHash = filterLog.TransactionHash
                });
            }
            else if (filterLog.IsLogForEvent<WithdrawnEventDTO>())
            {
                var decoded = filterLog.DecodeEvent<WithdrawnEventDTO>();
                await repository.AddWithdrawalAsync(new PoolWithdrawalEventData
                {
                    SpentNullifier = decoded.Event.SpentNullifier,
                    NewCommitment = decoded.Event.NewCommitment,
                    Value = decoded.Event.Value,
                    BlockNumber = (BigInteger)filterLog.BlockNumber.Value,
                    TransactionHash = filterLog.TransactionHash
                });
            }
            else if (filterLog.IsLogForEvent<RagequitEventDTO>())
            {
                var decoded = filterLog.DecodeEvent<RagequitEventDTO>();
                await repository.AddRagequitAsync(new PoolRagequitEventData
                {
                    Commitment = decoded.Event.Commitment,
                    Label = decoded.Event.Label,
                    Value = decoded.Event.Value,
                    BlockNumber = (BigInteger)filterLog.BlockNumber.Value,
                    TransactionHash = filterLog.TransactionHash
                });
            }
            else if (filterLog.IsLogForEvent<LeafInsertedEventDTO>())
            {
                var decoded = filterLog.DecodeEvent<LeafInsertedEventDTO>();
                await repository.AddLeafAsync(new PoolLeafEventData
                {
                    Leaf = decoded.Event.Leaf,
                    Index = decoded.Event.Index,
                    Root = decoded.Event.Root
                });

                if (stateTree != null)
                {
                    stateTree.InsertCommitment(decoded.Event.Leaf);
                }
            }
        }
    }
}
