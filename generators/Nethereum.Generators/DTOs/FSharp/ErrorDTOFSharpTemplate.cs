using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOFSharpTemplate : ClassTemplateBase<ErrorDTOModel>
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiErrorDtoFSharpTemplate;
        public ErrorDTOFSharpTemplate(ErrorDTOModel errorDTOModel) : base(errorDTOModel)
        {
            _parameterAbiErrorDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{SpaceUtils.OneTab}[<Error(""{Model.ErrorABI.Name}"")>]
{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{_parameterAbiErrorDtoFSharpTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.OneTab}";
            }
            return null;
        }
    }

}