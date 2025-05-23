using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOCSharpTemplate: ClassTemplateBase
    {
        public FunctionOutputDTOModel Model => (FunctionOutputDTOModel)ClassModel;

        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;
        public FunctionOutputDTOCSharpTemplate(FunctionOutputDTOModel model):base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}[FunctionOutput]
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : IFunctionOutputDTO 
{SpaceUtils.One__Tab}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.FunctionABI.OutputParameters)}
{SpaceUtils.One__Tab}}}";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}