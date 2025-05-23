using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ServiceVbTemplate : ClassTemplateBase
    {
        public ServiceModel Model => (ServiceModel)ClassModel;
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
{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}
{_deploymentServiceMethodsVbTemplate.GenerateMethods()}
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Protected Property Web3 As Nethereum.Web3.IWeb3
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Property ContractHandler As ContractHandler
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Sub New(ByVal web3 As Nethereum.Web3.Web3, ByVal contractAddress As String)
{SpaceUtils.Three____Tabs}Web3 = web3
{SpaceUtils.Three____Tabs}ContractHandler = web3.Eth.GetContractHandler(contractAddress)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Public Sub New(ByVal web3 As Nethereum.Web3.IWeb3, ByVal contractAddress As String)
{SpaceUtils.Three____Tabs}Web3 = web3
{SpaceUtils.Three____Tabs}ContractHandler = web3.Eth.GetContractHandler(contractAddress)
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.One__Tab}
{_functionServiceMethodVbTemplate.GenerateMethods()}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";

        }

    }
}