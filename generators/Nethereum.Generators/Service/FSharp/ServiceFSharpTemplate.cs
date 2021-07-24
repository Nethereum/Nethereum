using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ServiceFSharpTemplate : ClassTemplateBase<ServiceModel>
    {
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
{SpaceUtils.OneTab}type {Model.GetTypeName()} (web3: Web3, contractAddress: string) =
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}member val Web3 = web3 with get
{SpaceUtils.TwoTabs}member val ContractHandler = web3.Eth.GetContractHandler(contractAddress) with get
{SpaceUtils.OneTab}
{_deploymentServiceMethodsFSharpTemplate.GenerateMethods()}
{SpaceUtils.OneTab}
{_functionServiceMethodFSharpTemplate.GenerateMethods()}
{SpaceUtils.OneTab}";

        }
    }
}