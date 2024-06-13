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
            if (Model.HasParameters())
            {
                return
                    $@"{SpaceUtils.One__Tab}[<Error(""{Model.ErrorABI.Name}"")>]
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{SpaceUtils.Two___Tabs}inherit ErrorDTO()
{_parameterAbiErrorDtoFSharpTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.One__Tab}";
            }
            else
            {
               return $@"{SpaceUtils.One__Tab}[<Error(""{Model.ErrorABI.Name}"")>]
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{SpaceUtils.Two___Tabs}inherit ErrorDTO()
{SpaceUtils.One__Tab}";
            }
        }
    }

}