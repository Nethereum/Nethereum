using System;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class FunctionABIModel
    {
        public FunctionABI FunctionABI { get; }
        public CodeGenLanguage CodeGenLanguage { get; }
        private ITypeConvertor _abiTypeToDotnetTypeConvertor;
        public string MudNamespacePrefix { get; set; } = null;

        public FunctionABIModel(FunctionABI functionABI, ITypeConvertor abiTypeToDotnetTypeConvertor, CodeGenLanguage codeGenLanguage)
        {
            FunctionABI = functionABI;
            CodeGenLanguage = codeGenLanguage;
            this._abiTypeToDotnetTypeConvertor = abiTypeToDotnetTypeConvertor;
            
        }

        public string GetSingleOutputReturnType()
        {
            if (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length == 1)
            {
                var parameterModel = new ParameterABIModel(FunctionABI.OutputParameters[0], CodeGenLanguage);
                
                return _abiTypeToDotnetTypeConvertor.Convert(parameterModel.Parameter.Type,  
                    parameterModel.GetStructTypeClassName(), true);
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
            return (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length > 1) || 
                (FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length ==1 && 
                FunctionABI.OutputParameters[0].Type.StartsWith("tuple")) ;
        }

        public bool IsSingleOutput()
        {
            return FunctionABI.OutputParameters != null && FunctionABI.OutputParameters.Length == 1 && !FunctionABI.OutputParameters[0].Type.StartsWith("tuple");
        }
  
        public bool HasNoInputParameters()
        {
            return FunctionABI.InputParameters == null || FunctionABI.InputParameters.Length == 0;
        }

        public bool HasNoReturn()
        {
            return FunctionABI.OutputParameters == null || FunctionABI.OutputParameters.Length == 0;
        }

        public bool IsTransaction()
        {
            return FunctionABI.Constant == false;
        }

        public string GetFunctionSignatureName()
        {
            if(!string.IsNullOrEmpty(MudNamespacePrefix))
            {
                return MudNamespacePrefix + "__" + FunctionABI.Name;
            }
            return FunctionABI.Name;
        }
    }
}