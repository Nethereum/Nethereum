using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeFSharpTemplate : ClassTemplateBase<StructTypeModel>
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;
        public StructTypeFSharpTemplate(StructTypeModel model) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
                return
                    $@"{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.StructTypeABI.InputParameters)}
{SpaceUtils.OneTab}";

        }
    }
}