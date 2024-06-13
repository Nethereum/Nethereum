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

{SpaceUtils.One__Tab}[Error(""{Model.ErrorABI.Name}"")]
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : IErrorDTO
{SpaceUtils.One__Tab}{{
{_parameterAbiErrorDtocSharpTemplate.GenerateAllProperties(Model.ErrorABI.InputParameters)}
{SpaceUtils.One__Tab}}}";
            }
            else
            {
                return
                    $@"{GetPartialMainClass()}
{SpaceUtils.One__Tab}[Error(""{Model.ErrorABI.Name}"")]
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : IErrorDTO
{SpaceUtils.One__Tab}{{
{SpaceUtils.One__Tab}}}";
            }
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}