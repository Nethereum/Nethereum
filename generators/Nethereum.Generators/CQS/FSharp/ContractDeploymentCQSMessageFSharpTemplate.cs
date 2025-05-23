using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageFSharpTemplate : ClassTemplateBase
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;

        public ContractDeploymentCQSMessageFSharpTemplate(ContractDeploymentCQSMessageModel model) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(model, this);
        }
        public ContractDeploymentCQSMessageModel Model => (ContractDeploymentCQSMessageModel)ClassModel;

        public override string GenerateClass()
        {
            var typeName = Model.GetTypeName();
            return
                $@"{SpaceUtils.One__Tab}type {typeName}(byteCode: string) =
{SpaceUtils.Two___Tabs}inherit ContractDeploymentMessage(byteCode)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}static let BYTECODE = ""{Model.ByteCode}""
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}new() = {typeName}(BYTECODE)
{SpaceUtils.Two___Tabs}
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.One__Tab}";
        }

    }
}