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
        }
    }
}