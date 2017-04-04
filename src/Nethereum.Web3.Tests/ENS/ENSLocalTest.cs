using System.Linq;
using Nethereum.ENS;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.Web3.TransactionReceipts;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class ENSLocalTest
    {
        [Fact]
        public async void ShouldCreateEnsRegistarResolverAndRegiterandResolveANewAddress()
        {
            //The address we want to resolve when using "test.eth"
            var addressToResolve = "0x12890d2cce102216644c59dae5baed380d84830c";
           
            var defaultGas = new HexBigInteger(900000);

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";

            var web3 = new Web3(new ManagedAccount(addressFrom, pass), ClientFactory.GetClient());
            var web3Geth = new Web3Geth(ClientFactory.GetClient());

            var txService = new TransactionReceiptPollingService(web3);

            // var addressFrom = (await web3.Eth.Accounts.SendRequestAsync()).First();
            //uncomment to use geth instead of test-rpc
        
            await web3Geth.Miner.Start.SendRequestAsync();
            //deploy ENS contract
            var ensAddress = await txService.DeployContractAndGetAddressAsync(() => EnsService.DeployContractAsync(web3, addressFrom, defaultGas));
            
            var ensUtil = new EnsUtil();
            var ethNode = ensUtil.GetEnsNameHash("eth");
            
            //create a new First in First service registrar for "eth"
            var fifsAddress = await txService.DeployContractAndGetAddressAsync(() => FIFSRegistrarService.DeployContractAsync(web3, addressFrom, ensAddress, ethNode.HexToByteArray(),
                defaultGas));

          
            //create a public registry, which will allow us to find the registered address
            var publicResolverAddress =
                await
                    txService.DeployContractAndGetAddressAsync(
                        () =>
                            PublicResolverService.DeployContractAsync(web3, addressFrom, ensAddress,
                                defaultGas));


            var ensService = new EnsService(web3, ensAddress);

            //set ownership of "eth" to the fifs service
            //we are owners of "", so a subnode label "eth" will now be owned by the FIFS registar, which will allow to also to set ownership in Ens of further subnodes of Eth.
            var ethLabel = ensUtil.GetEnsLabelHash("eth");

            await txService.SendRequestAsync(() => ensService.SetSubnodeOwnerAsync(addressFrom, ensUtil.GetEnsNameHash("").HexToByteArray(),
                ethLabel.HexToByteArray(), fifsAddress, defaultGas));

            //Now the owner of Eth is the FIFS
            var ownerOfEth = await ensService.OwnerAsyncCall(ethNode.HexToByteArray());
            Assert.Equal(fifsAddress, ownerOfEth);
            /**** setup done **/

            //registration of "myname"
            
            //create a service for the registrar
            var fifsService = new FIFSRegistrarService(web3, fifsAddress);

            //create a label
            var testLabel = ensUtil.GetEnsLabelHash("myname");
            //submit the registration using the label bytes, and set ourselves as the owner
            await txService.SendRequestAsync(() => fifsService.RegisterAsync(addressFrom, testLabel.HexToByteArray(), addressFrom, defaultGas));

            //now using the the full name
            var fullNameNode = ensUtil.GetEnsNameHash("myname.eth");
            //set the resolver (the public one)
            await txService.SendRequestAsync(() => ensService.SetResolverAsync(addressFrom, fullNameNode.HexToByteArray(), publicResolverAddress, defaultGas));

            var publicResolverService = new PublicResolverService(web3, publicResolverAddress);
            // set the address in the resolver which we want to resolve, ownership is validated using ENS in the background
            await txService.SendRequestAsync(() => publicResolverService.SetAddrAsync(addressFrom, fullNameNode.HexToByteArray(), addressToResolve, defaultGas));

            //Now as "end user" we can start resolving... 

            //get the resolver address from ENS
            var resolverAddress = await ensService.ResolverAsyncCall(fullNameNode.HexToByteArray());

            //using the resolver address we can create our service (should be an abstract / interface based on abi as we can have many)
            var resolverService = new PublicResolverService(web3, resolverAddress);

            //and get the address from the resolver
            var theAddress = await resolverService.AddrAsyncCall(fullNameNode.HexToByteArray());
            Assert.Equal(addressToResolve, theAddress);

            await web3Geth.Miner.Stop.SendRequestAsync();

        }
    }
}