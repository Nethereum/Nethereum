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

namespace Nethereum.Mud.Contracts.Core.Tables
{

    public abstract class TableServiceBase<TTableRecordSingleton, TValue> : ITableServiceBase 
       where TTableRecordSingleton : TableRecordSingleton<TValue>, new()
       where TValue : class, new()
    {

        public TableServiceBase(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService)
        {
            WorldService = worldService;
            StoreEventsLogProcessingService = storeEventsLogProcessingService;
            RegistrationSystemService = registrationSystemService;
        }

        public TableServiceBase(IWeb3 web3, string contractAddress)
        {
            WorldService = new WorldService(web3, contractAddress);
            StoreEventsLogProcessingService = new StoreEventsLogProcessingService(web3, contractAddress);
            RegistrationSystemService = new RegistrationSystemService(web3, contractAddress);
        }

        public IResource Resource
        {
            get
            {
                return ResourceRegistry.GetResource<TTableRecordSingleton>();
            }
        }

        public WorldService WorldService { get; protected set; }
        public StoreEventsLogProcessingService StoreEventsLogProcessingService { get; protected set; }

        public RegistrationSystemService RegistrationSystemService { get; protected set; }

        public virtual Task<IEnumerable<TTableRecordSingleton>> GetRecordsFromLogsAsync(BigInteger? fromBlockNumber,
                                                                          BigInteger? toBlockNumber,
                                                                          CancellationToken cancellationToken,
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
    }
}
