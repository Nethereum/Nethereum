using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{

    public class StructTypeVbTemplate : ClassTemplateBase
    {
        public StructTypeModel Model => (StructTypeModel)ClassModel;

        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtoVbTemplate;
        public StructTypeVbTemplate(StructTypeModel model) : base(model)
        {
            _parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}
{_parameterAbiFunctionDtoVbTemplate.GenerateAllProperties(Model.StructTypeABI.InputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";

        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.One__Tab}End Class";

        }
    }
}

   