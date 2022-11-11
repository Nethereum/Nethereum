using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Console.Vb
{
    public class ConsoleVbTemplate: ClassTemplateBase<ConsoleModel>
    {
        private FunctionMockupMethodVbTemplate _functionMockupMethodVb;
        private ContractDeploymentMockUpMethodVbTemplate _contractDeploymentMockUpMethodVb;
        public ConsoleVbTemplate(ConsoleModel model):base(model)
        {
            _functionMockupMethodVb = new FunctionMockupMethodVbTemplate(model.ContractABI);
            _contractDeploymentMockUpMethodVb = new ContractDeploymentMockUpMethodVbTemplate(model.ContractDeploymentCQSMessageModel);
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
                $@"{SpaceUtils.OneTab}Public Module
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}Sub Main()
{SpaceUtils.ThreeTabs}'our entrypoint is RunAsync
{SpaceUtils.TwoTabs}End Sub
{SpaceUtils.TwoTabs}Public Async Function RunAsync() As Task
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim url = ""http://testchain.nethereum.com:8545""
{ SpaceUtils.ThreeTabs}' Dim url = ""https://mainnet.infura.io""
{SpaceUtils.ThreeTabs}Dim privateKey = ""0x7580e7fb49df1c861f0050fae31c2224c6aba908e116b8da44ee8cd927b990b0""
{SpaceUtils.ThreeTabs}Dim account = New Nethereum.Web3.Accounts.Account(privateKey)
{SpaceUtils.ThreeTabs}Dim web3 = New Web3(account, url)
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}
{_contractDeploymentMockUpMethodVb.GenerateMethods()}
{SpaceUtils.ThreeTabs}Dim contractHandler = web3.Eth.GetContractHandler(contractAddress)
{SpaceUtils.ThreeTabs}
{_functionMockupMethodVb.GenerateMethods()}
{SpaceUtils.TwoTabs}End Function
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}End Module";
        }
    }
}
