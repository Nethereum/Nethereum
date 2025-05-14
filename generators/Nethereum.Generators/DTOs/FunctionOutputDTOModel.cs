using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOModel: TypeMessageModel
    {
        public FunctionABI FunctionABI { get; }

        public FunctionOutputDTOModel(FunctionABI functionABI, string @namespace, string sharedTypesNamespace)
            :base(@namespace, functionABI.GetFunctionTypeNameBasedOnOverloads(), "OutputDTO")
        {
            FunctionABI = functionABI;
            InitialiseNamespaceDependencies(sharedTypesNamespace);
        }

        private void InitialiseNamespaceDependencies(string sharedTypesNamespace)
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", sharedTypesNamespace });
        }

        public bool CanGenerateOutputDTO()
        {
            return (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 0 &&
                   FunctionABI.Constant) ||
                   (new FunctionABIModel(FunctionABI, null, CodeGenLanguage).IsMultipleOutput());
        }
    }
}