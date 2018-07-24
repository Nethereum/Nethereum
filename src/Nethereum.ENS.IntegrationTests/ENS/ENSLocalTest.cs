using Nethereum.ENS.ENSRegistry.ContractDefinition;
using Nethereum.ENS.FIFSRegistrar.ContractDefinition;
using Nethereum.ENS.PublicResolver.ContractDefinition;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.ENS.IntegrationTests.ENS
{


    public class ENSMainNetTest
    {

        [Fact]
        public async void ShouldResolveNameFromMainnet()
        {
            var web3 = new Web3.Web3("https://mainnet.infura.io");

            var fullNameNode = new EnsUtil().GetNameHash("nickjohnson.eth");

            var ensRegistryService = new ENSRegistryService(web3, "0x314159265dd8dbb310642f98f50c066173c1259b");
            //get the resolver address from ENS
            var resolverAddress = await ensRegistryService.ResolverQueryAsync(
                new ResolverFunction() {Node = fullNameNode.HexToByteArray()});

            Assert.Equal("0x1da022710df5002339274aadee8d58218e9d6ab5", resolverAddress);
            //using the resolver address we can create our service (should be an abstract / interface based on abi as we can have many)
            var resolverService = new PublicResolverService(web3, resolverAddress);


            //and get the address from the resolver
            var theAddress =
                await resolverService.AddrQueryAsync(new AddrFunction() {Node = fullNameNode.HexToByteArray()});

            //Owner address
            var expectedAddress = "0xfdb33f8ac7ce72d7d4795dd8610e323b4c122fbb";
            Assert.Equal(expectedAddress.ToLower(), theAddress.ToLower());
        }

    }

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ENSLocalTest
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ENSLocalTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }


        [Fact]
        public async void ShouldCreateEnsRegistarResolverAndRegiterandResolveANewAddress()
        {
            //The address we want to resolve when using "test.eth"
            var addressToResolve = "0x12890d2cce102216644c59dae5baed380d84830c";


            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();


            //deploy ENS contract
            var ensDeploymentReceipt =
                await ENSRegistryService.DeployContractAndWaitForReceiptAsync(web3, new ENSRegistryDeployment());

            var ensUtil = new EnsUtil();
            var ethNode = ensUtil.GetNameHash("eth");

            //create a new First in First service registrar for "eth"
            var fifsDeploymentReceipt = await FIFSRegistrarService.DeployContractAndWaitForReceiptAsync(web3,
                new FIFSRegistrarDeployment()
                {
                    EnsAddr = ensDeploymentReceipt.ContractAddress,
                    Node = ethNode.HexToByteArray()
                });


            var publicResolverDeploymentReceipt = await PublicResolverService.DeployContractAndWaitForReceiptAsync(web3,
                new PublicResolverDeployment() {EnsAddr = ensDeploymentReceipt.ContractAddress}
            );




            var ensRegistryService = new ENSRegistryService(web3, ensDeploymentReceipt.ContractAddress);

            //set ownership of "eth" to the fifs service
            //we are owners of "", so a subnode label "eth" will now be owned by the FIFS registar, which will allow to also to set ownership in Ens of further subnodes of Eth.
            var ethLabel = ensUtil.GetLabelHash("eth");

            var receipt = await ensRegistryService.SetSubnodeOwnerRequestAndWaitForReceiptAsync(
                new SetSubnodeOwnerFunction()
                {
                    Node = ensUtil.GetNameHash("").HexToByteArray(),
                    Label = ethLabel.HexToByteArray(),
                    Owner = fifsDeploymentReceipt.ContractAddress
                }
            );


            //Now the owner of Eth is the FIFS
            var ownerOfEth =
                await ensRegistryService.OwnerQueryAsync(new OwnerFunction() {Node = ethNode.HexToByteArray()});
            Assert.Equal(fifsDeploymentReceipt.ContractAddress.ToLower(), ownerOfEth.ToLower());
            /**** setup done **/

            //registration of "myname"

            //create a service for the registrar
            var fifsService = new FIFSRegistrarService(web3, fifsDeploymentReceipt.ContractAddress);

            //create a label
            var testLabel = ensUtil.GetLabelHash("myname");
            //submit the registration using the label bytes, and set ourselves as the owner
            await fifsService.RegisterRequestAndWaitForReceiptAsync(new RegisterFunction()
            {
                Owner = addressFrom,
                Subnode = testLabel.HexToByteArray()
            });


            //now using the the full name
            var fullNameNode = ensUtil.GetNameHash("myname.eth");
            //set the resolver (the public one)
            await ensRegistryService.SetResolverRequestAndWaitForReceiptAsync(
                new SetResolverFunction()
                {
                    Resolver = publicResolverDeploymentReceipt.ContractAddress,
                    Node = fullNameNode.HexToByteArray()
                });


            var publicResolverService =
                new PublicResolverService(web3, publicResolverDeploymentReceipt.ContractAddress);
            // set the address in the resolver which we want to resolve, ownership is validated using ENS in the background


            await publicResolverService.SetAddrRequestAndWaitForReceiptAsync(
                new SetAddrFunction()
                {
                    Addr = addressToResolve,
                    Node = fullNameNode.HexToByteArray()
                });


            //Now as "end user" we can start resolving... 

            //get the resolver address from ENS
            var resolverAddress = await ensRegistryService.ResolverQueryAsync(
                new ResolverFunction() {Node = fullNameNode.HexToByteArray()});

            //using the resolver address we can create our service (should be an abstract / interface based on abi as we can have many)
            var resolverService = new PublicResolverService(web3, resolverAddress);

            //and get the address from the resolver
            var theAddress =
                await resolverService.AddrQueryAsync(new AddrFunction() {Node = fullNameNode.HexToByteArray()});

            Assert.Equal(addressToResolve.ToLower(), theAddress.ToLower());

        }
    }
}