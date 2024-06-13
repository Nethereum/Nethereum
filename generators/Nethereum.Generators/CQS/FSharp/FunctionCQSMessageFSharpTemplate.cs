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
                header = $@"{SpaceUtils.One__Tab}[<Function(""{functionABI.Name}"", typeof<{_functionOutputDTOModel.GetTypeName()}>)>]";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.One__Tab}[<Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")>]";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.One__Tab}[<Function(""{functionABI.Name}"")>]";
            }   

            return $@"{header}
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() = 
{SpaceUtils.Two___Tabs}inherit FunctionMessage()
{SpaceUtils.One__Tab}
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.One__Tab}";
        }

    }
}