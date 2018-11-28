using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageCSharpTemplate : ClassTemplateBase<ContractDeploymentCQSMessageModel>
    {
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public ContractDeploymentCQSMessageCSharpTemplate(ContractDeploymentCQSMessageModel model) : base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var typeName = Model.GetTypeName();
            return
                $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}public class {typeName}Base : ContractDeploymentMessage
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}public static string BYTECODE = ""{Model.ByteCode}"";
{SpaceUtils.TwoTabs}public {typeName}Base() : base(BYTECODE) {{ }}
{SpaceUtils.TwoTabs}public {typeName}Base(string byteCode) : base(byteCode) {{ }}
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }

        public string GetPartialMainClass()
        {
            var typeName = Model.GetTypeName();

            return $@"{SpaceUtils.OneTab}public partial class {typeName} : {typeName}Base
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}public {typeName}() : base(BYTECODE) {{ }}
{SpaceUtils.TwoTabs}public {typeName}(string byteCode) : base(byteCode) {{ }}
{SpaceUtils.OneTab}}}";
        }
    }
}