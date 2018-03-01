using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageModel
    {
        private CommonGenerators commonGenerators;

        public FunctionCQSMessageModel()
        {
            commonGenerators = new CommonGenerators();
        }

        public string GetFunctionMessageTypeName(FunctionABI functionABI)
        {
            return GetFunctionMessageTypeName(functionABI.Name);
        }

        public string GetFunctionMessageTypeName(string functionName)
        {
            return $"{commonGenerators.GenerateClassName(functionName)}Function";
        }

        public string GetFunctionMessageVariableName(FunctionABI functionABI)
        {
            return GetFunctionMessageVariableName(functionABI.Name);
        }

        public string GetFunctionMessageVariableName(string functionName)
        {
            return $"{commonGenerators.GenerateVariableName(functionName)}Function";
        }
    }
}