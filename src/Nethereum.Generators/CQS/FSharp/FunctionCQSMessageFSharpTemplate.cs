using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageFSharpTemplate : ClassTemplateBase<FunctionCQSMessageModel>
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionABIModel _functionABIModel;

        public FunctionCQSMessageFSharpTemplate(FunctionCQSMessageModel model, FunctionOutputDTOModel functionOutputDTOModel, FunctionABIModel functionABIModel) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            _functionOutputDTOModel = functionOutputDTOModel;
            _functionABIModel = functionABIModel;
            ClassFileTemplate = new FSharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var functionABI = Model.FunctionABI;
            var header = "";
            if (_functionABIModel.IsMultipleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[<Function(""{functionABI.Name}"", typeof<{_functionOutputDTOModel.GetTypeName()}>)>]";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[<Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")>]";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.OneTab}[<Function(""{functionABI.Name}"">]";
            }

            return $@"{header}
{SpaceUtils.OneTab}type {Model.GetTypeName()}() = 
{SpaceUtils.TwoTabs}inherit ContractMessage()
{SpaceUtils.OneTab}
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.OneTab}";
        }

    }
}