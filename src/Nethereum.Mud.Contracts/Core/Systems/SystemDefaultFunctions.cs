using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.ABI.Model;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public static class FunctionABIMudExtensions
    {
        public static FunctionABI CreateNewFunctionABIForMudNamespace(this FunctionABI functionABI, string @namespace)
        {
            if(string.IsNullOrEmpty(@namespace))
            {
                return functionABI;
            }
            var newName = $"{@namespace}__{functionABI.Name}";
            var newFunctionAbi = new FunctionABI(newName, functionABI.Constant);
            newFunctionAbi.InputParameters = functionABI.InputParameters;
            newFunctionAbi.OutputParameters = functionABI.OutputParameters;
            return newFunctionAbi;
        }
    }

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

        public string[] GetAllFunctionSignatures(string @namespace = null)
        {
            var functionAbis = GetAllFunctionABIs();
            if (string.IsNullOrEmpty(@namespace))
            {
                return GetAllFunctionABIs().Select(x => x.Sha3Signature).ToArray();
            }
            else
            {
                var newFunctionAbis = new List<FunctionABI>();
                foreach (var functionAbi in functionAbis)
                {
                    newFunctionAbis.Add(functionAbi.CreateNewFunctionABIForMudNamespace(@namespace));
                }
                return newFunctionAbis.Select(x => x.Sha3Signature).ToArray();
            }
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
