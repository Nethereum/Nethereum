
using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud.Contracts.World.Systems.RegistrationSystem
{
    public static class RegistrationMappingExtensions
    {

        public static RegisterTableFunction ToRegisterTableFunction(this SchemaEncoded schemaEncoded)
        {
            return new RegisterTableFunction()
            {
                KeySchema = schemaEncoded.KeySchema,
                ValueSchema = schemaEncoded.ValueSchema,
                TableId = schemaEncoded.TableId,
                FieldLayout = schemaEncoded.FieldLayout,
                FieldNames = schemaEncoded.FieldNames,
                KeyNames = schemaEncoded.KeyNames,

            };
        }

        public static SystemCallData ToBatchSystemCallData(this RegisterTableFunction registerTableFunction)
        {
            return new SystemCallData()
            {
                CallData = registerTableFunction.GetCallData(),
                SystemId = ResourceRegistry.GetResourceEncoded<RegistrationSystemResource>()
            };
        }

        public static SystemCallData ToRegisterTableFunctionBatchSystemCallData(this SchemaEncoded schemaEncoded)
        {
           return schemaEncoded.ToRegisterTableFunction().ToBatchSystemCallData();
        }

    }
}
