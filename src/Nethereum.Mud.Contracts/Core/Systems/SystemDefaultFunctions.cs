using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.ABI.Model;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Mud.Contracts.Core.Systems
{


    public class SystemDefaultFunctions
    {
        public List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MsgSenderFunction),
                typeof(MsgValueFunction),
                typeof(WorldFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public List<FunctionABI> GetAllFunctionABIs()
        {
            return GetAllFunctionTypes().Select(x => ABITypedRegistry.GetFunctionABI(x)).ToList();
        }

        public string[] GetAllFunctionSignatures()
        {
            return GetAllFunctionABIs().Select(x => x.Sha3Signature).ToArray();
        }

        public partial class MsgSenderFunction : MsgSenderFunctionBase { }

        [Function("_msgSender", "address")]
        public class MsgSenderFunctionBase : FunctionMessage
        {

        }

        public partial class MsgValueFunction : MsgValueFunctionBase { }

        [Function("_msgValue", "uint256")]
        public class MsgValueFunctionBase : FunctionMessage
        {

        }

        public partial class WorldFunction : WorldFunctionBase { }

        [Function("_world", "address")]
        public class WorldFunctionBase : FunctionMessage
        {

        }

        public partial class IncrementFunction : IncrementFunctionBase { }

        [Function("increment", "uint32")]
        public class IncrementFunctionBase : FunctionMessage
        {

        }

        public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

        [Function("supportsInterface", "bool")]
        public class SupportsInterfaceFunctionBase : FunctionMessage
        {
            [Parameter("bytes4", "interfaceId", 1)]
            public virtual byte[] InterfaceId { get; set; }
        }
    }
}
