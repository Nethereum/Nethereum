using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageModel:TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }
   
        public string CLASSNAME_SUFFIX = "Function";

        public FunctionCQSMessageModel(FunctionABI functionABI, string @namespace):base(@namespace)
        {
            FunctionABI = functionABI;
         
        }

        protected override string GetClassNameSuffix()
        {
            return CLASSNAME_SUFFIX;
        }

        protected override string GetBaseName()
        {
            return FunctionABI.Name;
        }
    }
}