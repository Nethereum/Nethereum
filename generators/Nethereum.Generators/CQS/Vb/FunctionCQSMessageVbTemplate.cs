using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageVbTemplate : ClassTemplateBase
    {
        public FunctionCQSMessageModel Model => (FunctionCQSMessageModel)ClassModel;

        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtovbTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionABIModel _functionABIModel;

        public FunctionCQSMessageVbTemplate(FunctionCQSMessageModel model, FunctionOutputDTOModel functionOutputDTOModel, FunctionABIModel functionABIModel) : base(model)
        {
            _parameterAbiFunctionDtovbTemplate = new ParameterABIFunctionDTOVbTemplate();
            _functionOutputDTOModel = functionOutputDTOModel;
            _functionABIModel = functionABIModel;
            ClassFileTemplate = new VbClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var functionABI = Model.FunctionABI;
            var header = "";
            if (_functionABIModel.IsMultipleOutput())
            {
                header = $@"{SpaceUtils.One__Tab}<[Function](""{functionABI.Name}"", GetType({_functionOutputDTOModel.GetTypeName()}))>";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.One__Tab}<[Function](""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")>";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.One__Tab}<[Function](""{functionABI.Name}"")>";
            }

            return $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}{header}
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Inherits FunctionMessage
{SpaceUtils.One__Tab}
{_parameterAbiFunctionDtovbTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class
";
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.One__Tab}End Class";

        }

    }
}