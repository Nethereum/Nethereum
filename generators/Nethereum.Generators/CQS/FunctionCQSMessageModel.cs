using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageModel:TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }
        
        public FunctionCQSMessageModel(FunctionABI functionABI, string @namespace, string sharedTypesNamespace) :
            base(@namespace, functionABI.GetFunctionTypeNameBasedOnOverloads(), "Function")
        {
            FunctionABI = functionABI;
            InitialiseNamespaceDependencies(sharedTypesNamespace);
        }

        private void InitialiseNamespaceDependencies(string sharedTypesNamespace)
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts", "Nethereum.ABI.FunctionEncoding.Attributes", sharedTypesNamespace });
        }


    }
}
