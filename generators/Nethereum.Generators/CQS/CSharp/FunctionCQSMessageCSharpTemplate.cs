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
            ClassFileTemplate = new CSharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var functionABI = Model.FunctionABI;
            var header = "";
            if (_functionABIModel.IsMultipleOutput())
            {
                header = $@"{SpaceUtils.One__Tab}[Function(""{functionABI.Name}"", typeof({_functionOutputDTOModel.GetTypeName()}))]";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.One__Tab}[Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")]";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.One__Tab}[Function(""{functionABI.Name}"")]";
            }

            return $@"{GetPartialMainClass()}

{header}
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : FunctionMessage
{SpaceUtils.One__Tab}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.One__Tab}}}";
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }


}