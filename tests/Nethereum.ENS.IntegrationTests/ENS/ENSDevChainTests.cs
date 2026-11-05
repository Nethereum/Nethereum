using System.Linq;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.ENS.ENSRegistry.ContractDefinition;
using Nethereum.ENS.FIFSRegistrar.ContractDefinition;
using Nethereum.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.ENS.IntegrationTests.ENS
{
    public class ENSDevChainTests
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string AddressToResolve = "0x12890D2cce102216644c59daE5baed380d84830c";

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "ens-contract-services", "Deploy ENS, a first-in-first-served registrar and a resolver, register and resolve a name")]
        public async Task ShouldDeployEnsRegisterAndResolveANameOnAnInProcessChain()
        {
            using var node = await DevChainNode.CreateAndStartAsync(new EthECKey(PrivateKey));
            var web3 = (Web3.Web3)node.CreateWeb3(PrivateKey);

            var ensRegistryService = await ENSRegistryService.DeployContractAndGetServiceAsync(web3, new ENSRegistryDeployment());

            var ensUtil = new EnsUtil();
            var ethNode = ensUtil.GetNameHash("eth");

            var fifsRegistrarService = await FIFSRegistrarService.DeployContractAndGetServiceAsync(web3,
                new FIFSRegistrarDeployment
                {
                    EnsAddr = ensRegistryService.ContractHandler.ContractAddress,
                    Node = ethNode.HexToByteArray()
                });

            var publicResolverService = await PublicResolverService.DeployContractAndGetServiceAsync(web3,
                new PublicResolverDeployment { Ens = ensRegistryService.ContractHandler.ContractAddress });

            await ensRegistryService.SetSubnodeOwnerRequestAndWaitForReceiptAsync(
                ensUtil.GetNameHash("").HexToByteArray(),
                ensUtil.GetLabelHash("eth").HexToByteArray(),
                fifsRegistrarService.ContractHandler.ContractAddress);

            await fifsRegistrarService.RegisterRequestAndWaitForReceiptAsync(new RegisterFunction
            {
                Owner = AddressToResolve,
                Subnode = ensUtil.GetLabelHash("myname").HexToByteArray()
            });

            var fullNameNode = ensUtil.GetNameHash("myname.eth").HexToByteArray();

            await ensRegistryService.SetResolverRequestAndWaitForReceiptAsync(new SetResolverFunction
            {
                Resolver = publicResolverService.ContractHandler.ContractAddress,
                Node = fullNameNode
            });

            await publicResolverService.SetAddrRequestAndWaitForReceiptAsync(fullNameNode, AddressToResolve);

            var resolverAddress = await ensRegistryService.ResolverQueryAsync(fullNameNode);
            var resolvedAddress = await new PublicResolverService(web3, resolverAddress).AddrQueryAsync(fullNameNode);

            Assert.Equal(AddressToResolve.ToLower(), resolvedAddress.ToLower());
            Assert.Equal(AddressToResolve.ToLower(),
                (await ensRegistryService.OwnerQueryAsync(fullNameNode)).ToLower());
        }
    }
}
