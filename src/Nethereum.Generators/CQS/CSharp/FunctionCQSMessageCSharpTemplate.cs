using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageCSharpTemplate : ClassTemplateBase<FunctionCQSMessageModel>
    {
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionABIModel _functionABIModel;
        
        public FunctionCQSMessageCSharpTemplate(FunctionCQSMessageModel model, FunctionOutputDTOModel functionOutputDTOModel, FunctionABIModel functionABIModel):base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            _functionOutputDTOModel = functionOutputDTOModel;
            _functionABIModel = functionABIModel;
            ClassFileTemplate = new CsharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var functionABI = Model.FunctionABI;
            var header = "";
            if (_functionABIModel.IsMultipleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", typeof({_functionOutputDTOModel.GetTypeName()}))]";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")]";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}""]";
            }

            return $@"{header}
{SpaceUtils.OneTab}public class {Model.GetTypeName()}:ContractMessage
{SpaceUtils.OneTab}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }
            
    }
}