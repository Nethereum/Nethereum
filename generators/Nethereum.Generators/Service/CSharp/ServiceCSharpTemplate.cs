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
                $@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()}: ContractWeb3ServiceBase
{SpaceUtils.OneTab}{{
{_deploymentServiceMethodsCSharpTemplate.GenerateMethods()}
{SpaceUtils.NoTabs}
{SpaceUtils.TwoTabs}public {Model.GetTypeName()}(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}
{SpaceUtils.NoTabs}
{_functionServiceMethodCSharpTemplate.GenerateMethods()}
{SpaceUtils.OneTab}}}";
        }
    }
}