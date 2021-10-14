using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOModel : TypeMessageModel
    {
        public ErrorABI ErrorABI { get; }

        public ErrorDTOModel(ErrorABI errorABI, string @namespace)
            : base(@namespace, errorABI.GetErrorTypeNameBasedOnOverloads(), "Error")
        {
            ErrorABI = errorABI;
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes" });
        }

        public bool CanGenerateOutputDTO()
        {
            return ErrorABI.InputParameters != null && ErrorABI.InputParameters.Length > 0;
        }

    }
}