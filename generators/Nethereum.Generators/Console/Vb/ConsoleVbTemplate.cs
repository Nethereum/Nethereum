using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Console.Vb
{
    public class ConsoleVbTemplate: ClassTemplateBase
    {
        private FunctionMockupMethodVbTemplate _functionMockupMethodVb;
        private ContractDeploymentMockUpMethodVbTemplate _contractDeploymentMockUpMethodVb;
        public ConsoleVbTemplate(ConsoleModel model):base(model)
        {
            _functionMockupMethodVb = new FunctionMockupMethodVbTemplate(model.ContractABI);
            _contractDeploymentMockUpMethodVb = new ContractDeploymentMockUpMethodVbTemplate(model.ContractDeploymentCQSMessageModel);
            ClassFileTemplate = new VbClassFileTemplate(ClassModel, this);
        }

        public override string GenerateClass()
        {
            return
                $@"{SpaceUtils.One__Tab}Public Module
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}Sub Main()
{SpaceUtils.Three____Tabs}'our entrypoint is RunAsync
{SpaceUtils.Two___Tabs}End Sub
{SpaceUtils.Two___Tabs}Public Async Function RunAsync() As Task
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim url = ""http://testchain.nethereum.com:8545""
{ SpaceUtils.Three____Tabs}' Dim url = ""https://mainnet.infura.io""
{SpaceUtils.Three____Tabs}Dim privateKey = ""0x7580e7fb49df1c861f0050fae31c2224c6aba908e116b8da44ee8cd927b990b0""
{SpaceUtils.Three____Tabs}Dim account = New Nethereum.Web3.Accounts.Account(privateKey)
{SpaceUtils.Three____Tabs}Dim web3 = New Web3(account, url)
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}
{_contractDeploymentMockUpMethodVb.GenerateMethods()}
{SpaceUtils.Three____Tabs}Dim contractHandler = web3.Eth.GetContractHandler(contractAddress)
{SpaceUtils.Three____Tabs}
{_functionMockupMethodVb.GenerateMethods()}
{SpaceUtils.Two___Tabs}End Function
{SpaceUtils.NoTabs}
{SpaceUtils.One__Tab}End Module";
        }
    }
}
