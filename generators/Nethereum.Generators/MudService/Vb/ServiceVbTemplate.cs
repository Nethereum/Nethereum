using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.MudService
{
    public class MudServiceVbTemplate : ClassTemplateBase
    {
        public MudServiceModel Model => (MudServiceModel)ClassModel;
        public MudServiceVbTemplate(MudServiceModel model) : base(model)
        {
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            var deploymentName = Model.ContractDeploymentCQSMessageModel.GetTypeName();
            var resourceName = Model.GetResourceClassName();
            var serviceName = Model.GetTypeName();

            return
                $@"
{GenerateSystemClass()}
{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits ContractWeb3ServiceBase
{SpaceUtils.Two___Tabs}Public ReadOnly Property Resource As IResource
{SpaceUtils.Three____Tabs}Get
{SpaceUtils.Four_____Tabs}Return Me.GetResource()
{SpaceUtils.Three____Tabs}End Get
{SpaceUtils.Two___Tabs}End Property
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}Public ReadOnly Property SystemServiceResourceRegistrator As ISystemServiceResourceRegistration
{SpaceUtils.Three____Tabs}Get
{SpaceUtils.Four_____Tabs}Return Me.GetSystemServiceResourceRegistration(Of {resourceName}, {serviceName})()
{SpaceUtils.Three____Tabs}End Get
{SpaceUtils.Three____Tabs}End Property
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}Public Function GetSystemFunctionABIs() As List(Of FunctionABI)
{SpaceUtils.Three____Tabs}Return GetAllFunctionABIs()
{SpaceUtils.Two___Tabs}End Function
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}Public Function CalculateCreate2Address(deployerAddress As String, salt As String, ParamArray byteCodeLibraries As ByteCodeLibrary()) As String
{SpaceUtils.Three____Tabs}Return New {deploymentName}().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries)
{SpaceUtils.Two___Tabs}End Function
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}Public Function DeployCreate2ContractAsync(deployerAddress As String, salt As String, ParamArray byteCodeLibraries As ByteCodeLibrary()) As Task(Of Create2ContractDeploymentTransactionResult)
{SpaceUtils.Three____Tabs}Dim create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService
{SpaceUtils.Three____Tabs}Dim deployment = New {deploymentName}()
{SpaceUtils.Three____Tabs}Return create2ProxyDeployerService.DeployContractRequestAsync(deployment, deployerAddress, salt, byteCodeLibraries)
{SpaceUtils.Two___Tabs}End Function
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}Public Function DeployCreate2ContractAndWaitForReceiptAsync(deployerAddress As String, salt As String, byteCodeLibraries As ByteCodeLibrary(), Optional cancellationToken As CancellationToken = Nothing) As Task(Of Create2ContractDeploymentTransactionReceiptResult)
{SpaceUtils.Three____Tabs}Dim create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService
{SpaceUtils.Three____Tabs}Dim deployment = New {deploymentName}()
{SpaceUtils.Three____Tabs}Return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(deployment, deployerAddress, salt, byteCodeLibraries, cancellationToken)
{SpaceUtils.Two___Tabs}End Function
{SpaceUtils.One__Tab}End Class";
        }

        public string GenerateSystemClass()
        {
            if (string.IsNullOrEmpty(Model.MudNamespace))
            {
                return $@"{SpaceUtils.One__Tab}Public Class {Model.GetResourceClassName()} Inherits SystemResource
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Public Sub New() 
{SpaceUtils.Three____Tabs}MyBase.New(""{Model.GetSystemName()}"")
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.One__Tab}End Class";
            }
            else
            {
                return $@"{SpaceUtils.One__Tab}Public Class {Model.GetResourceClassName()} Inherits SystemResource
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Public Sub New() 
{SpaceUtils.Three____Tabs}MyBase.New(""{Model.GetSystemName()}"", ""{Model.MudNamespace}"")
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.One__Tab}End Class";
            }
        }
    }

}