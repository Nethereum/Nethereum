using Nethereum.BlockchainProcessing.Services;
using Nethereum.Web3;
using Nethereum.Contracts.Services;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Mud.TableRepository;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using Nethereum.Hex.HexConvertors.Extensions;


namespace Nethereum.Mud.Contracts.StoreEvents
{
    public class StoreEventsLogProcessingService
        {
        private readonly IBlockchainLogProcessingService _blockchainLogProcessing;
        private readonly IEthApiContractService _ethApiContractService;

        public StoreEventsLogProcessingService(IWeb3 web3)
        {
            _blockchainLogProcessing = web3.Processing.Logs;
            _ethApiContractService = web3.Eth;
        }



        public Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForContract(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<StoreSetRecordEventDTO>(contractAddresses: new[] { contractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTableAndContract(string contractAddress, byte[] tableId,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var filterInputTo = new FilterInputBuilder<StoreSetRecordEventDTO>().AddTopic(x => x.TableId, tableId)
              .Build(contractAddress);
            return await _blockchainLogProcessing.GetAllEvents<StoreSetRecordEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

           
        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTableAndContract(string contractAddress, string nameSpace, string tableName,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            return await GetAllSetRecordForTableAndContract(contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTableAndContract(string contractAddress, string tableName,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            return await GetAllSetRecordForTableAndContract(contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForContract(string contractAddress,
           BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
           int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<StoreSpliceStaticDataEventDTO>(contractAddresses: new[] { contractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTableAndContract(string contractAddress, byte[] tableId,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var filterInputTo = new FilterInputBuilder<StoreSetRecordEventDTO>().AddTopic(x => x.TableId, tableId)
              .Build(contractAddress);
            return await _blockchainLogProcessing.GetAllEvents<StoreSpliceStaticDataEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTableAndContract(string contractAddress, string nameSpace, string tableName,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            return await GetAllSpliceStaticDataForTableAndContract(contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTableAndContract(string contractAddress, string tableName,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            return await GetAllSpliceStaticDataForTableAndContract(contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }


        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string contractAddress, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                       int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var topics = new List<object>
                {
                    Event<StoreSetRecordEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreSpliceStaticDataEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreSpliceDynamicDataEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreDeleteRecordEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()
                };

            var filterInput = new NewFilterInput()
            {
                Address = new[] { contractAddress },
                Topics = new object[] { topics.ToArray() }
            };

            await ProcessAllStoreChangesAsync(tableRepository, fromBlockNumber, toBlockNumber, numberOfBlocksPerRequest, retryWeight, filterInput, cancellationToken);

        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string contractAddress, string nameSpace, string tableName, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                  int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            await ProcessAllStoreChangesAsync(tableRepository, contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string contractAddress, string tableName, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                             int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            await ProcessAllStoreChangesAsync(tableRepository, contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string contractAddress, byte[] tableId, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                                        int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var topics = new List<object>
            {
                    Event<StoreSetRecordEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreSpliceStaticDataEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreSpliceDynamicDataEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                    Event<StoreDeleteRecordEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()
                };

            var filterInput = new NewFilterInput()
            {
                Address = new[] { contractAddress },
                Topics = new object[] { topics.ToArray(), new object[] { new Bytes32TypeEncoder().Encode(tableId).ToHex(true) } }
            };

            await ProcessAllStoreChangesAsync(tableRepository, fromBlockNumber, toBlockNumber, numberOfBlocksPerRequest, retryWeight, filterInput, cancellationToken);
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsFromLogsAsync<TTableRecord>(string contractAddress, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                       int retryWeight = BlockchainLogProcessingService.RetryWeight)
            where TTableRecord : ITableRecord, new()
        {
            var tableId = new TTableRecord().ResourceId;
            var tableRepository = new InMemoryTableRepository();
            await ProcessAllStoreChangesAsync(tableRepository, contractAddress, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
            return await tableRepository.GetTableRecordsAsync<TTableRecord>(tableId);
        }

        private async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, int numberOfBlocksPerRequest, int retryWeight, NewFilterInput filterInput, CancellationToken cancellationToken)
        {
            var logs = await _blockchainLogProcessing.GetAllEvents(filterInput, fromBlockNumber, toBlockNumber,
                              cancellationToken, numberOfBlocksPerRequest, retryWeight);

            foreach (var log in logs)
            {
                if (log.IsLogForEvent<StoreSetRecordEventDTO>())
                {
                    var setRecordEventLog = log.DecodeEvent<StoreSetRecordEventDTO>();
                    var setRecordEvent = setRecordEventLog.Event;
                    await tableRepository.SetRecordAsync(setRecordEvent.TableId, setRecordEvent.KeyTuple, setRecordEvent.StaticData, setRecordEvent.EncodedLengths, setRecordEvent.DynamicData);
                }

                if (log.IsLogForEvent<StoreSpliceStaticDataEventDTO>())
                {
                    var spliceStaticDataEventLog = log.DecodeEvent<StoreSpliceStaticDataEventDTO>();
                    var spliceStaticDataEvent = spliceStaticDataEventLog.Event;
                    await tableRepository.SetSpliceStaticDataAsync(spliceStaticDataEvent.TableId, spliceStaticDataEvent.KeyTuple, spliceStaticDataEvent.Start, spliceStaticDataEvent.Data);
                }

                if (log.IsLogForEvent<StoreSpliceDynamicDataEventDTO>())
                {
                    var spliceDynamicDataEventLog = log.DecodeEvent<StoreSpliceDynamicDataEventDTO>();
                    var spliceDynamicDataEvent = spliceDynamicDataEventLog.Event;
                    await tableRepository.SetSpliceDynamicDataAsync(spliceDynamicDataEvent.TableId, spliceDynamicDataEvent.KeyTuple, spliceDynamicDataEvent.Start, spliceDynamicDataEvent.Data, spliceDynamicDataEvent.DeleteCount, spliceDynamicDataEvent.EncodedLengths);
                }

                if(log.IsLogForEvent<StoreDeleteRecordEventDTO>())
                {
                    var deleteRecordEventLog = log.DecodeEvent<StoreDeleteRecordEventDTO>();
                    var deleteRecordEvent = deleteRecordEventLog.Event;
                    await tableRepository.DeleteRecordAsync(deleteRecordEvent.TableId, deleteRecordEvent.KeyTuple);
                }
            }
        }
    }
}
