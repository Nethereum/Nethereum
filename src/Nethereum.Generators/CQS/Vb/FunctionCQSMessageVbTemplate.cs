using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageVbTemplate : ClassTemplateBase<FunctionCQSMessageModel>
    {
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
                header = $@"{SpaceUtils.OneTab}<[Function](""{functionABI.Name}"", GetType({_functionOutputDTOModel.GetTypeName()}))>";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.OneTab}<[Function](""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")>";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.OneTab}<[Function](""{functionABI.Name}"")>";
            }

            return $@"{header}
{SpaceUtils.OneTab}Public Class {Model.GetTypeName()}
{SpaceUtils.TwoTabs}Inherits ContractMessage
{SpaceUtils.OneTab}
{_parameterAbiFunctionDtovbTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class
";
        }

    }
}