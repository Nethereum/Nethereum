using Nethereum.ENS.ENSRegistry.ContractDefinition;
using Nethereum.ENS.FIFSRegistrar.ContractDefinition;
using Nethereum.ENS.PublicResolver.ContractDefinition;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.XUnitEthereumClients;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.ENS.IntegrationTests.ENS
{
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
            //Ignoring parity due to https://github.com/paritytech/parity-ethereum/issues/8675
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                //The address we want to resolve when using "test.eth"
                var addressToResolve = "0x12890D2cce102216644c59daE5baed380d84830c";


                var addressFrom = "0x12890D2cce102216644c59daE5baed380d84830c";

                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                //CI: slowing polling for CI
                web3.TransactionManager.TransactionReceiptService =
                    new TransactionReceiptPollingService(web3.TransactionManager, 500);


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


                var publicResolverDeploymentReceipt = await PublicResolverService.DeployContractAndWaitForReceiptAsync(
                    web3,
                    new PublicResolverDeployment() {Ens = ensDeploymentReceipt.ContractAddress}
                );


                var ensRegistryService = new ENSRegistryService(web3, ensDeploymentReceipt.ContractAddress);

                //set ownership of "eth" to the fifs service
                //we are owners of "", so a subnode label "eth" will now be owned by the FIFS registar, which will allow to also to set ownership in Ens of further subnodes of Eth.
                var ethLabel = ensUtil.GetLabelHash("eth");

                var receipt = await ensRegistryService.SetSubnodeOwnerRequestAndWaitForReceiptAsync(
                    ensUtil.GetNameHash("").HexToByteArray(),
                    ethLabel.HexToByteArray(),
                    fifsDeploymentReceipt.ContractAddress
                );


                //Now the owner of Eth is the FIFS
                var ownerOfEth =
                    await ensRegistryService.OwnerQueryAsync(ethNode.HexToByteArray());
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

                var ownerOfMyName =
                    await ensRegistryService.OwnerQueryAsync(fullNameNode.HexToByteArray());
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

                //Fails here
                await publicResolverService.SetAddrRequestAndWaitForReceiptAsync(fullNameNode.HexToByteArray(),
                    addressToResolve
                );


                //Now as "end user" we can start resolving... 

                //get the resolver address from ENS
                var resolverAddress = await ensRegistryService.ResolverQueryAsync(
                    fullNameNode.HexToByteArray());

                //using the resolver address we can create our service (should be an abstract / interface based on abi as we can have many)
                var resolverService = new PublicResolverService(web3, resolverAddress);

                //and get the address from the resolver
                var theAddress =
                    await resolverService.AddrQueryAsync(fullNameNode.HexToByteArray());

                Assert.Equal(addressToResolve.ToLower(), theAddress.ToLower());
            }

        }
    }
}
