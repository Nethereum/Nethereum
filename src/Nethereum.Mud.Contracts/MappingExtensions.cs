using Nethereum.Mud.Contracts.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.EncodingDecoding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Mud.Contracts
{
    public static class MappingExtensions
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
       
    }
}
