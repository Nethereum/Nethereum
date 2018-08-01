using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ServiceVbTemplate : ClassTemplateBase<ServiceModel>
    {
        private FunctionServiceMethodVbTemplate _functionServiceMethodVbTemplate;
        private ContractDeploymentServiceMethodsVbTemplate _deploymentServiceMethodsVbTemplate;
        public ServiceVbTemplate(ServiceModel model) : base(model)
        {
            _functionServiceMethodVbTemplate = new FunctionServiceMethodVbTemplate(model);
            _deploymentServiceMethodsVbTemplate = new ContractDeploymentServiceMethodsVbTemplate(model);
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
                $@"
{SpaceUtils.OneTab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}
{_deploymentServiceMethodsVbTemplate.GenerateMethods()}
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}Protected Property Web3 As Nethereum.Web3.Web3
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Property ContractHandler As ContractHandler
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Sub New(ByVal web3 As Nethereum.Web3.Web3, ByVal contractAddress As String)
{SpaceUtils.ThreeTabs}Web3 = web3
{SpaceUtils.ThreeTabs}ContractHandler = web3.Eth.GetContractHandler(contractAddress)
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.OneTab}
{_functionServiceMethodVbTemplate.GenerateMethods()}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";

        }
    }
}