using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Console.CSharp
{
    public class ConsoleCSharpTemplate: ClassTemplateBase<ConsoleModel>
    {
        private FunctionMockupMethodCSharpTemplate _functionMockupMethodCSharp;
        private ContractDeploymentMockUpMethodCSharpTemplate _deploymentMockUpMethodCSharpTemplate;
        public ConsoleCSharpTemplate(ConsoleModel model):base(model)
        {
            _functionMockupMethodCSharp = new FunctionMockupMethodCSharpTemplate(model.ContractABI);
            _deploymentMockUpMethodCSharpTemplate = new ContractDeploymentMockUpMethodCSharpTemplate(model.ContractDeploymentCQSMessageModel);
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
                $@"{SpaceUtils.OneTab}public class {Model.GetTypeName()}
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}public static async Task Main()
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var url = ""http://testchain.nethereum.com:8545"";
{SpaceUtils.ThreeTabs}//var url = ""https://mainnet.infura.io"";
{SpaceUtils.ThreeTabs}var privateKey = ""0x7580e7fb49df1c861f0050fae31c2224c6aba908e116b8da44ee8cd927b990b0"";
{SpaceUtils.ThreeTabs}var account = new Nethereum.Web3.Accounts.Account(privateKey);
{SpaceUtils.ThreeTabs}var web3 = new Web3(account, url);
{SpaceUtils.ThreeTabs}
{_deploymentMockUpMethodCSharpTemplate.GenerateMethods()}
{SpaceUtils.ThreeTabs}var contractHandler = web3.Eth.GetContractHandler(contractAddress);
{_functionMockupMethodCSharp.GenerateMethods()}
{SpaceUtils.TwoTabs}}}
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}}}";
        }
    }
}
