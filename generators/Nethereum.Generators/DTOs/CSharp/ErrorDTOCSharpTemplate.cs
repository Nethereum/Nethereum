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
            if (Model.HasParameters())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}[Error(""{Model.ErrorABI.Name}"")]
{SpaceUtils.OneTab}public class {Model.GetTypeName()}Base : IErrorDTO
{SpaceUtils.OneTab}{{
{_parameterAbiErrorDtocSharpTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
            }
            else
            {
                return
                    $@"{GetPartialMainClass()}
{SpaceUtils.OneTab}[Error(""{Model.ErrorABI.Name}"")]
{SpaceUtils.OneTab}public class {Model.GetTypeName()}Base : IErrorDTO
{SpaceUtils.OneTab}{{
{SpaceUtils.OneTab}}}";
            }
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}