using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOModel : TypeMessageModel
    {
        public ErrorABI ErrorABI { get; }

        public ErrorDTOModel(ErrorABI errorABI, string @namespace, string sharedTypesNamespace)
            : base(@namespace, errorABI.GetErrorTypeNameBasedOnOverloads(), "Error")
        {
            ErrorABI = errorABI;
            InitisialiseNamespaceDependencies(sharedTypesNamespace);
        }

        private void InitisialiseNamespaceDependencies(string sharedTypesNamespace)
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", sharedTypesNamespace });
        }

        public bool HasParameters()
        {
            return ErrorABI.InputParameters != null && ErrorABI.InputParameters.Length > 0;
        }

    }
}