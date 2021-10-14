using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOCSharpTemplate : ClassTemplateBase<ErrorDTOModel>
    {
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiErrorDtocSharpTemplate;
        public ErrorDTOCSharpTemplate(ErrorDTOModel errorDTOModel) : base(errorDTOModel)
        {
            _parameterAbiErrorDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}[Error(""{Model.ErrorABI.Name}"")]
{SpaceUtils.OneTab}public class {Model.GetTypeName()}Base
{SpaceUtils.OneTab}{{
{_parameterAbiErrorDtocSharpTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}