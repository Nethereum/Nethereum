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
                $@"{SpaceUtils.One__Tab}public class {Model.GetTypeName()}
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public static async Task Main()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var url = ""http://testchain.nethereum.com:8545"";
{SpaceUtils.Three____Tabs}//var url = ""https://mainnet.infura.io"";
{SpaceUtils.Three____Tabs}var privateKey = ""0x7580e7fb49df1c861f0050fae31c2224c6aba908e116b8da44ee8cd927b990b0"";
{SpaceUtils.Three____Tabs}var account = new Nethereum.Web3.Accounts.Account(privateKey);
{SpaceUtils.Three____Tabs}var web3 = new Web3(account, url);
{SpaceUtils.Three____Tabs}
{_deploymentMockUpMethodCSharpTemplate.GenerateMethods()}
{SpaceUtils.Three____Tabs}var contractHandler = web3.Eth.GetContractHandler(contractAddress);
{_functionMockupMethodCSharp.GenerateMethods()}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.NoTabs}
{SpaceUtils.One__Tab}}}";
        }
    }
}
