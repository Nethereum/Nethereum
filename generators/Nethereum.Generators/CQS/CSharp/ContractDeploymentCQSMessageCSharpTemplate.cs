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

{SpaceUtils.One__Tab}public class {typeName}Base : ContractDeploymentMessage
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public static string BYTECODE = ""{Model.ByteCode}"";
{SpaceUtils.Two___Tabs}public {typeName}Base() : base(BYTECODE) {{ }}
{SpaceUtils.Two___Tabs}public {typeName}Base(string byteCode) : base(byteCode) {{ }}
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.One__Tab}}}";
        }

        public string GetPartialMainClass()
        {
            var typeName = Model.GetTypeName();

            return $@"{SpaceUtils.One__Tab}public partial class {typeName} : {typeName}Base
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public {typeName}() : base(BYTECODE) {{ }}
{SpaceUtils.Two___Tabs}public {typeName}(string byteCode) : base(byteCode) {{ }}
{SpaceUtils.One__Tab}}}";
        }
    }
}