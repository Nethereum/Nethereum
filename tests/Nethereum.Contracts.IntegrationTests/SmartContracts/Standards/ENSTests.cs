using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multiformats.Codec;
using Multiformats.Hash;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts.Standards
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ENSMainNetTest
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ENSMainNetTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public async void ShouldBeAbleToRegisterExample()
        {
            var durationInDays = 365;
            var ourName = "lllalalalal"; //enter owner name
            var tls = "eth";
            var owner = "0x111F530216fBB0377B4bDd4d303a465a1090d09d";
            var secret = "Today is gonna be the day That theyre gonna throw it back to you"; //make your own


            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ethTLSService = web3.Eth.GetEnsEthTlsService();
            await ethTLSService.InitialiseAsync();

            var price = await ethTLSService.CalculateRentPriceInEtherAsync(ourName, durationInDays);
            Assert.True(price > 0);

            var commitment = await ethTLSService.CalculateCommitmentAsync(ourName, owner, secret);
            var commitTransactionReceipt = await ethTLSService.CommitRequestAndWaitForReceiptAsync(commitment);
            var txnHash = await ethTLSService.RegisterRequestAsync(ourName, owner, durationInDays, secret, price);
        }

        public async void ShouldBeAbleToSetTextExample()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var txn = await ensService.SetTextRequestAsync("nethereum.eth", TextDataKey.url, "https://nethereum.com");
        }

        [Fact]
        public async void ShouldBeAbleToResolveText()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var url = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.url);
            Assert.Equal("https://nethereum.com", url);
        }

        [Fact]
        public async void ShouldBeAbleToCalculateRentPriceAndCommitment()
        {
            var durationInDays = 365;
            var ourName = "supersillynameformonkeys";
            var tls = "eth";
            var owner = "0x12890D2cce102216644c59daE5baed380d84830c";
            var secret = "animals in the forest";


            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ethTLSService = web3.Eth.GetEnsEthTlsService();
            await ethTLSService.InitialiseAsync();

            var price = await ethTLSService.CalculateRentPriceInEtherAsync(ourName, durationInDays);
            Assert.True(price > 0);

            var commitment = await ethTLSService.CalculateCommitmentAsync(ourName, owner, secret);
            Assert.Equal("0x546d078db03381f4a33a33600cf1b91e00815b572c944f4a19624c8d9aaa9c14", commitment.ToHex(true));
        }


        [Fact]
        public async void ShouldFindEthControllerFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ethTLSService = web3.Eth.GetEnsEthTlsService();
            await ethTLSService.InitialiseAsync();
            var controllerAddress = ethTLSService.TLSControllerAddress;
            Assert.True("0x283Af0B28c62C092C9727F1Ee09c02CA627EB7F5".IsTheSameAddress(controllerAddress));

        }

        [Fact]
        public async void ShouldResolveAddressFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("nick.eth");
            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(theAddress));
        }

        //Food for thought, a simple CID just using IPFS Base58 Defaulting all other values / Swarm
        [Fact]
        public async void ShouldRetrieveTheContentHashAndDecodeIt()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var content = await ensService.GetContentHashAsync("3-7-0-0.web3.nethereum.dotnet.netdapps.eth");
            var storage = content[0];
            //This depends on IPLD.ContentIdentifier, Multiformats Hash and Codec
            if (storage == 0xe3) // if storage is IPFS 
            {
                //We skip 2 storage ++
                var cid = IPLD.ContentIdentifier.Cid.Cast(content.Skip(2).ToArray());
                var decoded = cid.Hash.B58String();
                Assert.Equal("QmRZiL8WbAVQMF1715fhG3b4x9tfGS6hgBLPQ6KYfKzcYL", decoded);
            }

        }

        [Fact]
        public async void ShouldCreateContentIPFSHash()
        {
            var multihash = Multihash.FromB58String("QmRZiL8WbAVQMF1715fhG3b4x9tfGS6hgBLPQ6KYfKzcYL");
            var cid = new IPLD.ContentIdentifier.Cid(MulticodecCode.MerkleDAGProtobuf, multihash);
            var ipfsStoragePrefix = new byte[] {0xe3, 0x01};
            var fullContentHash = ipfsStoragePrefix.Concat(cid.ToBytes()).ToArray();
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var content = await ensService.GetContentHashAsync("3-7-0-0.web3.nethereum.dotnet.netdapps.eth");
            //e301017012202febb4a7c84c8079f78844e50150d97ad33e2a3a0d680d54e7211e30ef13f08d
            Assert.Equal(content.ToHex(), fullContentHash.ToHex());
        }

        //[Fact]
        public async void ShouldSetSubnodeExample()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var txn = await ensService.SetSubnodeOwnerRequestAsync("yoursupername.eth", "subdomainName",
                "addressOwner");
        }

        [Fact]
        public async void ShouldReverseResolveAddressFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var name = await ensService.ReverseResolveAsync("0xd1220a0cf47c7b9be7a2e6ba89f429762e7b9adb");
            var expectedName = "alex.vandesande.eth";
            Assert.Equal(expectedName, name);
        }

    }
}
