using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeCSharpTemplate : ClassTemplateBase<StructTypeModel>
    {
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;
        public StructTypeCSharpTemplate(StructTypeModel model) : base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}public class {Model.GetTypeName()}Base 
{SpaceUtils.OneTab}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.StructTypeABI.InputParameters)}
{SpaceUtils.OneTab}}}";

        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}