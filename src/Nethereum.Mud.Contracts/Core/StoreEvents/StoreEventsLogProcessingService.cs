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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;


namespace Nethereum.Mud.Contracts.Core.StoreEvents
{
    public class StoreEventsLogProcessingService
    {
        protected readonly IBlockchainLogProcessingService _blockchainLogProcessing;
        protected readonly IEthApiContractService _ethApiContractService;
        protected readonly IWeb3 _web3;
        public string ContractAddress { get; protected set; }

        public StoreEventsLogProcessingService(IWeb3 web3, string contractAddress)
        {
            _blockchainLogProcessing = web3.Processing.Logs;
            _ethApiContractService = web3.Eth;
            _web3 = web3;
            ContractAddress = contractAddress;
        }



        public Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecord(
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<StoreSetRecordEventDTO>(contractAddresses: new[] { ContractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTable(byte[] tableId,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var filterInputTo = new FilterInputBuilder<StoreSetRecordEventDTO>().AddTopic(x => x.TableId, tableId)
              .Build(ContractAddress);
            return await _blockchainLogProcessing.GetAllEvents<StoreSetRecordEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);


        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTable(string nameSpace, string tableName,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            return await GetAllSetRecordForTable(tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public async Task<List<EventLog<StoreSetRecordEventDTO>>> GetAllSetRecordForTable(string tableName,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            return await GetAllSetRecordForTable(tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticData(
           BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
           int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<StoreSpliceStaticDataEventDTO>(new string[] { ContractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTable(byte[] tableId,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var filterInputTo = new FilterInputBuilder<StoreSetRecordEventDTO>().AddTopic(x => x.TableId, tableId)
              .Build(ContractAddress);
            return await _blockchainLogProcessing.GetAllEvents<StoreSpliceStaticDataEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTable(string nameSpace, string tableName,
             BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
             int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            return await GetAllSpliceStaticDataForTable(tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

        public async Task<List<EventLog<StoreSpliceStaticDataEventDTO>>> GetAllSpliceStaticDataForTable(string tableName,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)

        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            return await GetAllSpliceStaticDataForTable(tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }


        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
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
                Address = new[] { ContractAddress },
                Topics = new object[] { topics.ToArray() }
            };

            await ProcessAllStoreChangesAsync(tableRepository, fromBlockNumber, toBlockNumber, numberOfBlocksPerRequest, retryWeight, filterInput, cancellationToken);

        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string nameSpace, string tableName, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                  int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var tableId = ResourceEncoder.EncodeTable(nameSpace, tableName);
            await ProcessAllStoreChangesAsync(tableRepository, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, string tableName, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                             int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var tableId = ResourceEncoder.EncodeRootTable(tableName);
            await ProcessAllStoreChangesAsync(tableRepository, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public async Task ProcessAllStoreChangesAsync(ITableRepository tableRepository, byte[] tableId, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
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
                Address = new[] { ContractAddress },
                Topics = new object[] { topics.ToArray(), new object[] { new Bytes32TypeEncoder().Encode(tableId).ToHex(true) } }
            };

            await ProcessAllStoreChangesAsync(tableRepository, fromBlockNumber, toBlockNumber, numberOfBlocksPerRequest, retryWeight, filterInput, cancellationToken);
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsFromLogsAsync<TTableRecord>(BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                      int retryWeight = BlockchainLogProcessingService.RetryWeight)
           where TTableRecord : ITableRecordSingleton, new()
        {
            var tableId = new TTableRecord().ResourceIdEncoded;
            var tableRepository = new InMemoryTableRepository();
            await ProcessAllStoreChangesAsync(tableRepository, tableId, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
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

                if (log.IsLogForEvent<StoreDeleteRecordEventDTO>())
                {
                    var deleteRecordEventLog = log.DecodeEvent<StoreDeleteRecordEventDTO>();
                    var deleteRecordEvent = deleteRecordEventLog.Event;
                    await tableRepository.DeleteRecordAsync(deleteRecordEvent.TableId, deleteRecordEvent.KeyTuple);
                }
            }
        }
    }
}
