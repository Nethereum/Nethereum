using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOVbTemplate : ClassTemplateBase<FunctionOutputDTOModel>
    {
        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtoVbTemplate;
        public FunctionOutputDTOVbTemplate(FunctionOutputDTOModel model) : base(model)
        {
            _parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}<[FunctionOutput]>
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Implements IFunctionOutputDTO
{SpaceUtils.Two___Tabs}
{_parameterAbiFunctionDtoVbTemplate.GenerateAllProperties(Model.FunctionABI.OutputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.One__Tab}End Class";

        }
    }
}