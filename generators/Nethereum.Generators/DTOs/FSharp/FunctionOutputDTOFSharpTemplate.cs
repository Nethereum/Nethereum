using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOFSharpTemplate : ClassTemplateBase<FunctionOutputDTOModel>
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;
        public FunctionOutputDTOFSharpTemplate(FunctionOutputDTOModel model) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{SpaceUtils.OneTab}[<FunctionOutput>]
{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{SpaceUtils.TwoTabs}inherit FunctionOutputDTO() 
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.FunctionABI.OutputParameters)}
{SpaceUtils.OneTab}";
            }
            return null;
        }
    }
}