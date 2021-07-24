using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{

    public class StructTypeVbTemplate : ClassTemplateBase<StructTypeModel>
    {
        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtoVbTemplate;
        public StructTypeVbTemplate(StructTypeModel model) : base(model)
        {
            _parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.TwoTabs}
{_parameterAbiFunctionDtoVbTemplate.GenerateAllProperties(Model.StructTypeABI.InputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";

        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.TwoTabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.OneTab}End Class";

        }
    }
}

   