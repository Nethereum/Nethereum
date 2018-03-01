using System;
using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class FunctionABIModel
    {
        private ABITypeToCSharpType abiTypeToCSharpType;

        public FunctionABIModel()
        {
            this.abiTypeToCSharpType = new ABITypeToCSharpType();
        }

        public string GetSingleOutputGenericReturnType(FunctionABI item)
        {
            if (item == null) return String.Empty;
            return $"<{GetSingleOutputReturnType(item)}>";
        }

        public string GetSingleOutputReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return abiTypeToCSharpType.GetTypeMap(functionABI.OutputParameters[0].Type, true);
            }
            return null;
        }

        public string GetSingleAbiReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return functionABI.OutputParameters[0].Type;
            }
            return null;
        }

        public bool IsMultipleOutput(FunctionABI functionAbi)
        {
            return functionAbi.OutputParameters != null && functionAbi.OutputParameters.Length > 1;
        }

        public bool IsSingleOutput(FunctionABI functionAbi)
        {
            return functionAbi.OutputParameters != null && functionAbi.OutputParameters.Length == 1;
        }

        public bool HasNoReturn(FunctionABI functionAbi)
        {
            return functionAbi.OutputParameters == null || functionAbi.OutputParameters.Length == 0;
        }
    }
}