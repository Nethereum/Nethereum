using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Services;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using Nethereum.Mud.TableRepository;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem;
using Nethereum.Mud.Contracts.Store;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.Core.Tables
{

    public abstract class TableServiceBase<TTableRecordSingleton, TValue> : ITableServiceBase 
       where TTableRecordSingleton : TableRecordSingleton<TValue>, new()
       where TValue : class, new()
    {
        protected StoreEventsLogProcessingService StoreEventsLogProcessingService { get; set; }
        protected RegistrationSystemService RegistrationSystemService { get; set; }

        protected WorldService WorldService { get; set; }

        public TableServiceBase(WorldNamespace world)
        {
            StoreEventsLogProcessingService = new StoreEventsLogProcessingService(world.Web3, world.ContractAddress);
            RegistrationSystemService = world.Systems.RegistrationSystem;
            WorldService = world.WorldService;
        }

        public TableServiceBase(IWeb3 web3, string contractAddress)
        {
            RegistrationSystemService = new RegistrationSystemService(web3, contractAddress);
            StoreEventsLogProcessingService = new StoreEventsLogProcessingService(web3, contractAddress);
            WorldService = new WorldService(web3, contractAddress);
        }

        public IResource Resource
        {
            get
            {
                return ResourceRegistry.GetResource<TTableRecordSingleton>();
            }
        }

        public virtual Task<IEnumerable<TTableRecordSingleton>> GetRecordsFromLogsAsync(BigInteger? fromBlockNumber = null,
                                                                          BigInteger? toBlockNumber = null,
                                                                          CancellationToken cancellationToken = default,
                                                                          int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                                                          int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return StoreEventsLogProcessingService.GetTableRecordsFromLogsAsync<TTableRecordSingleton>(fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public virtual Task<IEnumerable<TTableRecordSingleton>> GetRecordsFromRepository(ITableRepository repository)
        {
            return repository.GetTableRecordsAsync<TTableRecordSingleton>();
        }

        public SchemaEncoded GetSchemaEncoded()
        {
            return ResourceRegistry.GetResource<TTableRecordSingleton>().GetSchemaEncoded();
        }
        
        public SystemCallData GetRegisterTableFunctionBatchSystemCallData()
        {
            var schemaEncoded = GetSchemaEncoded();
            return schemaEncoded.ToRegisterTableFunctionBatchSystemCallData();
        }

        public virtual Task<string> RegisterTableRequestAsync()
        {
            var schemaEncoded = GetSchemaEncoded();
            return RegistrationSystemService.RegisterTableRequestAsync(schemaEncoded.ToRegisterTableFunction());
        }

        public virtual Task<TransactionReceipt> RegisterTableRequestAndWaitForReceiptAsync()
        {
            var schemaEncoded = GetSchemaEncoded();
            return RegistrationSystemService.RegisterTableRequestAndWaitForReceiptAsync(schemaEncoded.ToRegisterTableFunction());
        }
    }
}
