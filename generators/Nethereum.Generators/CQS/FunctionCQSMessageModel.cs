using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageModel:TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }
        
        public FunctionCQSMessageModel(FunctionABI functionABI, string @namespace):
            base(@namespace, functionABI.GetFunctionTypeNameBasedOnOverloads(), "Function")
        {
            FunctionABI = functionABI;
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts", "Nethereum.ABI.FunctionEncoding.Attributes" });
        }


    }
}
