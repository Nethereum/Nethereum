using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOVbTemplate : ClassTemplateBase<ErrorDTOModel>
    {
        private ParameterABIFunctionDTOVbTemplate _parameterAbiErrorDtoVbTemplate;
        public ErrorDTOVbTemplate(ErrorDTOModel errorDTOModel) : base(errorDTOModel)
        {
            _parameterAbiErrorDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}<[Error](""{Model.ErrorABI.Name}"")>
{SpaceUtils.OneTab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.TwoTabs}Implements IErrorDTO
{SpaceUtils.TwoTabs}
{_parameterAbiErrorDtoVbTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.TwoTabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.OneTab}End Class";

        }
    }
}