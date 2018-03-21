using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOModel: TypeMessageModel
    {
        private const string SUFFIX_NAME = "OutputDTO";
        public FunctionABI FunctionABI { get; }

        public FunctionOutputDTOModel(FunctionABI functionABI, string @namespace):base(@namespace)
        {
            FunctionABI = functionABI;
        }
        
        public bool CanGenerateOutputDTO()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 0 &&
                   FunctionABI.Constant;
        }

        protected override string GetClassNameSuffix()
        {
            return SUFFIX_NAME;
        }

        protected override string GetBaseName()
        {
            return FunctionABI.Name;
        }
    }
}