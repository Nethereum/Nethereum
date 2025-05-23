using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOFSharpTemplate : ClassTemplateBase
    {
        public FunctionOutputDTOModel Model => (FunctionOutputDTOModel)ClassModel;

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
                    $@"{SpaceUtils.One__Tab}[<FunctionOutput>]
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{SpaceUtils.Two___Tabs}inherit FunctionOutputDTO() 
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.FunctionABI.OutputParameters)}
{SpaceUtils.One__Tab}";
            }
            return null;
        }
    }
}