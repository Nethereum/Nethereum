using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.TableRepository;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Mud.Contracts.Core.Tables
{
    public interface ITableServiceBase
    {
        IResource Resource { get; }
        StoreEventsLogProcessingService StoreEventsLogProcessingService { get; }

        SystemCallData GetRegisterTableFunctionBatchSystemCallData();
        SchemaEncoded GetSchemaEncoded();
        Task<string> RegisterTableRequestAsync();
    }
}