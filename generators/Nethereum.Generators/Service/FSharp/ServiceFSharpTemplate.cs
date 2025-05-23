using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ServiceFSharpTemplate : ClassTemplateBase
    {
        public ServiceModel Model => (ServiceModel)ClassModel;
        private FunctionServiceMethodFSharpTemplate _functionServiceMethodFSharpTemplate;
        private ContractDeploymentServiceMethodsFSharpTemplate _deploymentServiceMethodsFSharpTemplate;
        public ServiceFSharpTemplate(ServiceModel model) : base(model)
        {
            _functionServiceMethodFSharpTemplate = new FunctionServiceMethodFSharpTemplate(model);
            _deploymentServiceMethodsFSharpTemplate = new ContractDeploymentServiceMethodsFSharpTemplate(model);
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }
        public override string GenerateClass()
        {
            return
                $@"
{SpaceUtils.One__Tab}type {Model.GetTypeName()} (web3: Web3, contractAddress: string) =
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}member val Web3 = web3 with get
{SpaceUtils.Two___Tabs}member val ContractHandler = web3.Eth.GetContractHandler(contractAddress) with get
{SpaceUtils.One__Tab}
{_deploymentServiceMethodsFSharpTemplate.GenerateMethods()}
{SpaceUtils.One__Tab}
{_functionServiceMethodFSharpTemplate.GenerateMethods()}
{SpaceUtils.One__Tab}";

        }
    }
}