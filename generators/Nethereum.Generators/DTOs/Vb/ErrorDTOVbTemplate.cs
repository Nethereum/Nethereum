using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOVbTemplate : ClassTemplateBase
    {
        public ErrorDTOModel Model => (ErrorDTOModel)ClassModel;
        private ParameterABIFunctionDTOVbTemplate _parameterAbiErrorDtoVbTemplate;
        public ErrorDTOVbTemplate(ErrorDTOModel errorDTOModel) : base(errorDTOModel)
        {
            _parameterAbiErrorDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.HasParameters())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}<[Error](""{Model.ErrorABI.Name}"")>
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Implements IErrorDTO
{SpaceUtils.Two___Tabs}
{_parameterAbiErrorDtoVbTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
            }
            else
            {
               return $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}<[Error](""{Model.ErrorABI.Name}"")>
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Implements IErrorDTO
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
            }
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.One__Tab}End Class";

        }
    }
}