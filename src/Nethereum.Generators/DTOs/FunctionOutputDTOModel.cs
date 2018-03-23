using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOModel: TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }

        public FunctionOutputDTOModel(FunctionABI functionABI, string @namespace)
            :base(@namespace, functionABI.Name, "OutputDTO")
        {
            FunctionABI = functionABI;
        }
        
        public bool CanGenerateOutputDTO()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 0 &&
                   FunctionABI.Constant;
        }
    }
}