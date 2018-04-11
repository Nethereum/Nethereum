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
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes" });
        }

        public bool CanGenerateOutputDTO()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 0 &&
                   FunctionABI.Constant;
        }
    }
}