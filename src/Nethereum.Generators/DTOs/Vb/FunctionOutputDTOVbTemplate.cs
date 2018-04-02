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
                    $@"{SpaceUtils.OneTab}<[FunctionOutput]>
{SpaceUtils.OneTab}Public Class {Model.GetTypeName()}
{SpaceUtils.OneTab}
{_parameterAbiFunctionDtoVbTemplate.GenerateAllProperties(Model.FunctionABI.OutputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";
            }
            return null;
        }
    }
}