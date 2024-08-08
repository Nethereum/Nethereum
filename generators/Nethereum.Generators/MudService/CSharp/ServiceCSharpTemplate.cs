using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.MudService
{
    public class MudServiceCSharpTemplate : ClassTemplateBase<MudServiceModel>
    {
        public MudServiceCSharpTemplate(MudServiceModel model) : base(model)
        {
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            var deploymentName = Model.ContractDeploymentCQSMessageModel.GetTypeName();
            var resourceName = Model.GetResourceClassName();
            var serviceName = Model.GetTypeName();

            return
$@"{GenerateSystemClass()}
{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()}: ISystemService<{resourceName}> 
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public IResource Resource => this.GetResource();
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}get
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Four_____Tabs}return this.GetSystemServiceResourceRegistration<{resourceName}, {serviceName}>();
{SpaceUtils.Three____Tabs}}}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public List<FunctionABI> GetSystemFunctionABIs()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return GetAllFunctionABIs();
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new {deploymentName}().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
{SpaceUtils.Three____Tabs}var deployment = new {deploymentName}();
{SpaceUtils.Three____Tabs}return create2ProxyDeployerService.DeployContractRequestAsync(deployment, deployerAddress, salt, byteCodeLibraries);
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
{SpaceUtils.Three____Tabs}var deployment = new {deploymentName}();
{SpaceUtils.Three____Tabs}return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(deployment, deployerAddress, salt, byteCodeLibraries, cancellationToken);
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.One__Tab}}}";
        }

        public string GenerateSystemClass()
        {
            if (string.IsNullOrEmpty(Model.MudNamespace))
            {
                return
      $@"{SpaceUtils.One__Tab}public class {Model.GetResourceClassName()} : SystemResource
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public {Model.GetResourceClassName()}() : base(""{Model.GetSystemName()}"") {{ }}
{SpaceUtils.One__Tab}}}";
            }
            else
            {
                return
     $@"{SpaceUtils.One__Tab}public class {Model.GetResourceClassName()} : SystemResource
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public {Model.GetResourceClassName()}() : base(""{Model.GetSystemName()}"", ""{Model.MudNamespace}"") {{ }}
{SpaceUtils.One__Tab}}}";
            }

        }
    }
}