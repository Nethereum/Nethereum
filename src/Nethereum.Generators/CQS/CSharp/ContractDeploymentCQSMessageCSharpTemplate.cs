using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageCSharpTemplate: ClassTemplateBase<ContractDeploymentCQSMessageModel>
    {
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public ContractDeploymentCQSMessageCSharpTemplate(ContractDeploymentCQSMessageModel model):base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var typeName = Model.GetTypeName();
            return
$@"{SpaceUtils.OneTab}public class {typeName}:ContractDeploymentMessage
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public static string BYTECODE = ""{Model.ByteCode}"";
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}():base(BYTECODE) {{ }}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}(string byteCode):base(byteCode) {{ }}
{SpaceUtils.TwoTabs}
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }

    }
}