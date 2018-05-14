using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ServiceCSharpTemplate: ClassTemplateBase<ServiceModel>
    {
        private FunctionServiceMethodCSharpTemplate _functionServiceMethodCSharpTemplate;
        private ContractDeploymentServiceMethodsCSharpTemplate _deploymentServiceMethodsCSharpTemplate;
        public ServiceCSharpTemplate(ServiceModel model):base(model)
        {
            _functionServiceMethodCSharpTemplate = new FunctionServiceMethodCSharpTemplate(model);
            _deploymentServiceMethodsCSharpTemplate = new ContractDeploymentServiceMethodsCSharpTemplate(model);
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
                $@"
{SpaceUtils.OneTab}public class {Model.GetTypeName()}
{SpaceUtils.OneTab}{{
{SpaceUtils.OneTab}
{_deploymentServiceMethodsCSharpTemplate.GenerateMethods()}
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}protected Web3.Web3 Web3{{ get; }}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}protected ContractHandler ContractHandler {{ get; }}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {Model.GetTypeName()}(Web3.Web3 web3, string contractAddress)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}Web3 = web3;
{SpaceUtils.ThreeTabs}ContractHandler = web3.Eth.GetContractHandler(contractAddress);
{SpaceUtils.TwoTabs}}}
{SpaceUtils.OneTab}
{_functionServiceMethodCSharpTemplate.GenerateMethods()}
{SpaceUtils.OneTab}}}";
           
        }
    }
}