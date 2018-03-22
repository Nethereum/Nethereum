using System;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class FunctionABIModel
    {
        public FunctionABI FunctionABI { get; }
        private ABITypeToCSharpType abiTypeToCSharpType;

        public FunctionABIModel(FunctionABI functionABI)
        {
            FunctionABI = functionABI;
            this.abiTypeToCSharpType = new ABITypeToCSharpType();
        }

        public string GetSingleOutputGenericReturnType()
        {
            if (FunctionABI == null) return String.Empty;
            return $"<{GetSingleOutputReturnType()}>";
        }

        public string GetSingleOutputReturnType()
        {
            if (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length == 1)
            {
                return abiTypeToCSharpType.GetTypeMap(FunctionABI.OutputParameters[0].Type, true);
            }
            return null;
        }

        public string GetSingleAbiReturnType()
        {
            if (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length == 1)
            {
                return FunctionABI.OutputParameters[0].Type;
            }
            return null;
        }

        public bool IsMultipleOutput()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 1;
        }

        public bool IsSingleOutput()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length == 1;
        }

        public bool HasNoReturn()
        {
            return FunctionABI.OutputParameters == null || FunctionABI.OutputParameters.Length == 0;
        }

        public bool IsTransaction()
        {
            return FunctionABI.Constant == false;
        }
    }
}