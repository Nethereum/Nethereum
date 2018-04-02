using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageModel:TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }
        
        public FunctionCQSMessageModel(FunctionABI functionABI, string @namespace):
            base(@namespace, functionABI.Name, "Function")
        {
            FunctionABI = functionABI;
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts.CQS", "Nethereum.ABI.FunctionEncoding.Attributes" });
        }


    }
}
