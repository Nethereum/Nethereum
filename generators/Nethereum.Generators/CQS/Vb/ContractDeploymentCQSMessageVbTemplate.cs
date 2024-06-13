using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageVbTemplate : ClassTemplateBase<ContractDeploymentCQSMessageModel>
    {
        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtovbTemplate;

        public ContractDeploymentCQSMessageVbTemplate(ContractDeploymentCQSMessageModel model) : base(model)
        {
            _parameterAbiFunctionDtovbTemplate = new ParameterABIFunctionDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(model, this);
        }

        public override string GenerateClass()
        {
            var typeName = Model.GetTypeName();
            return
                $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}Public Class {typeName}Base 
{SpaceUtils.Three____Tabs}Inherits ContractDeploymentMessage
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Shared DEFAULT_BYTECODE As String = ""{Model.ByteCode}""
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Sub New()
{SpaceUtils.Three____Tabs}MyBase.New(DEFAULT_BYTECODE)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Sub New(ByVal byteCode As String)
{SpaceUtils.Three____Tabs}MyBase.New(byteCode)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.Two___Tabs}
{_parameterAbiFunctionDtovbTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
        }

        public string GetPartialMainClass()
        {
            var typeName = Model.GetTypeName();

            return $@"{SpaceUtils.One__Tab}Public Partial Class {typeName}
{SpaceUtils.One__Tab} Inherits {typeName}Base
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Public Sub New()
{SpaceUtils.Three____Tabs}MyBase.New(DEFAULT_BYTECODE)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Sub New(ByVal byteCode As String)
{SpaceUtils.Three____Tabs}MyBase.New(byteCode)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
        }

    }
}