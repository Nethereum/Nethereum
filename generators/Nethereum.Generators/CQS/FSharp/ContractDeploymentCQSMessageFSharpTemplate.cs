using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageFSharpTemplate : ClassTemplateBase<ContractDeploymentCQSMessageModel>
    {
        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;

        public ContractDeploymentCQSMessageFSharpTemplate(ContractDeploymentCQSMessageModel model) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var typeName = Model.GetTypeName();
            return
                $@"{SpaceUtils.OneTab}type {typeName}(byteCode: string) =
{SpaceUtils.TwoTabs}inherit ContractDeploymentMessage(byteCode)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}static let BYTECODE = ""{Model.ByteCode}""
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}new() = {typeName}(BYTECODE)
{SpaceUtils.TwoTabs}
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.OneTab}";
        }

    }
}