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

{SpaceUtils.OneTab}Public Class {typeName}Base 
{SpaceUtils.ThreeTabs}Inherits ContractDeploymentMessage
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Shared DEFAULT_BYTECODE As String = ""{Model.ByteCode}""
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Sub New()
{SpaceUtils.ThreeTabs}MyBase.New(DEFAULT_BYTECODE)
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Sub New(ByVal byteCode As String)
{SpaceUtils.ThreeTabs}MyBase.New(byteCode)
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.TwoTabs}
{_parameterAbiFunctionDtovbTemplate.GenerateAllProperties(Model.ConstructorABI.InputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";
        }

        public string GetPartialMainClass()
        {
            var typeName = Model.GetTypeName();

            return $@"{SpaceUtils.OneTab}Public Partial Class 
{SpaceUtils.OneTab} Inherits {typeName}Base
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}Public Sub New()
{SpaceUtils.ThreeTabs}MyBase.New(DEFAULT_BYTECODE)
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Sub New(ByVal byteCode As String)
{SpaceUtils.ThreeTabs}MyBase.New(byteCode)
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";
        }

    }
}