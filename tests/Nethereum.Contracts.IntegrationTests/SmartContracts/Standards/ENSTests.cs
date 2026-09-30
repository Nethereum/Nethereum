using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ADRaffy.ENSNormalize;
using Multiformats.Codec;
using Multiformats.Hash;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ENS.Multicallable.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.UniversalResolver;

using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
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
            await ethTLSService.InitialiseAsync().ConfigureAwait(false);

            var price = await ethTLSService.CalculateRentPriceInEtherAsync(ourName, durationInDays).ConfigureAwait(false);
            Assert.True(price > 0);

            var commitment = await ethTLSService.CalculateCommitmentAsync(ourName, owner, secret).ConfigureAwait(false);
            var commitTransactionReceipt = await ethTLSService.CommitRequestAndWaitForReceiptAsync(commitment).ConfigureAwait(false);
            var txnHash = await ethTLSService.RegisterRequestAsync(ourName, owner, durationInDays, secret, price).ConfigureAwait(false);
        }

        public async void ShouldBeAbleToSetTextExample()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var txn = await ensService.SetTextRequestAsync("nethereum.eth", TextDataKey.url, "https://nethereum.com").ConfigureAwait(false);
        }

        //[Fact]
        //public async void ShouldBeAbleToResolveText()
        //{
        //    var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
        //    var ensService = web3.Eth.GetEnsService();
        //    var url = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.url).ConfigureAwait(false);
        //    Assert.Equal("https://nethereum.com", url);
        //}

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
            await ethTLSService.InitialiseAsync().ConfigureAwait(false);

            var price = await ethTLSService.CalculateRentPriceInEtherAsync(ourName, durationInDays).ConfigureAwait(false);
            Assert.True(price > 0);

            var commitment = await ethTLSService.CalculateCommitmentAsync(ourName, owner, secret).ConfigureAwait(false);
            Assert.Equal("0x546d078db03381f4a33a33600cf1b91e00815b572c944f4a19624c8d9aaa9c14", commitment.ToHex(true));
        }


        [Fact]
        public async void ShouldFindEthControllerFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ethTLSService = web3.Eth.GetEnsEthTlsService();
            await ethTLSService.InitialiseAsync().ConfigureAwait(false);
            var controllerAddress = ethTLSService.TLSControllerAddress;
            Assert.True("0x283Af0B28c62C092C9727F1Ee09c02CA627EB7F5".IsTheSameAddress(controllerAddress));

        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ENS forward resolution: name to address", SkillName = "built-in-standards", Order = 3)]
        public async void ShouldResolveAddressFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("nick.eth").ConfigureAwait(false);
            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(theAddress));
        }

        //Food for thought, a simple CID just using IPFS Base58 Defaulting all other values / Swarm
        [Fact]
        public async void ShouldRetrieveTheContentHashAndDecodeIt()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var content = await ensService.GetContentHashAsync("3-7-0-0.web3.nethereum.dotnet.netdapps.eth").ConfigureAwait(false);
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
            var content = await ensService.GetContentHashAsync("3-7-0-0.web3.nethereum.dotnet.netdapps.eth").ConfigureAwait(false);
            //e301017012202febb4a7c84c8079f78844e50150d97ad33e2a3a0d680d54e7211e30ef13f08d
            Assert.Equal(content.ToHex(), fullContentHash.ToHex());
        }

        //[Fact]
        public async void ShouldSetSubnodeExample()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var txn = await ensService.SetSubnodeOwnerRequestAsync("yoursupername.eth", "subdomainName",
                "addressOwner").ConfigureAwait(false);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ENS reverse resolution: address to name", SkillName = "built-in-standards", Order = 4)]
        public async void ShouldReverseResolveAddressFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var name = await ensService.ReverseResolveAsync("0xd1220a0cf47c7b9be7a2e6ba89f429762e7b9adb").ConfigureAwait(false);
            var expectedName = "alex.vandesande.eth";
            Assert.Equal(expectedName, name);
        }


        [Fact]
        public async void ShouldResolveAddressFromMainnetEmoji()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("💩💩💩.eth").ConfigureAwait(false);
            var expectedAddress = "0x372973309f827B5c3864115cE121c96ef9cB1658";
            Assert.True(expectedAddress.IsTheSameAddress(theAddress));
        }


        [Fact]
        public async void ShouldNormaliseAsciiDomain()
        {
            var input = "foo.eth"; // latin chars only
            var expected = "foo.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }


        [Fact]
        public void ShouldNotNormaliseMixtureOfCharactersDomain()
        {
            var input = "fоо.eth"; // with cyrillic 'o'
            var expected = "fоо.eth";

            Assert.Throws<InvalidLabelException>(() =>
                    new EnsUtil().Normalise(input));
            //Invalid label "fоо‎": illegal mixture: Latin + Cyrillic о‎ {43E}
        }

        [Fact]
        public void ShouldNormaliseToLowerDomain()
        {
            var input = "Foo.eth"; 
            var expected = "foo.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ShouldNormaliseEmojiDomain()
        {
            var input = "🦚.eth";
            var expected = "🦚.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }

        [Fact]
        public async void ShouldResolveAddressOffline()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);        
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("1.offchainexample.eth").ConfigureAwait(false);
            var expected = "0x41563129cDbbD0c5D3e1c86cf9563926b243834d";
            Assert.True(expected.IsTheSameAddress(theAddress));
        }


        [Fact]
        public async void ShouldResolveEmailOffline()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveTextAsync("1.offchainexample.eth", TextDataKey.email).ConfigureAwait(false);
            var expected = "nick@ens.domains";
            Assert.True(expected.IsTheSameAddress(theAddress));
        }

        [Fact]
        public async void ShouldResolveDescriptionOffline()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var description = await ensService.ResolveTextAsync("1.offchainexample.eth", TextDataKey.description).ConfigureAwait(false);
            var expected = "hello offchainresolver wildcard record";
            Assert.True(expected.IsTheSameAddress(description));
        }

        [Fact(Skip = "This is an example of resolving a name that is only stored offchain, so it will fail if the offchain resolver not running")]
        public async void ShouldResolveAddressOfflineMatoken()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("matoken.lens.xyz").ConfigureAwait(false);
            var expected = "0x5A384227B65FA093DEC03Ec34e111Db80A040615";
            Assert.True(expected.IsTheSameAddress(theAddress));
        }


        [Fact]
        public async void ShouldResolveAddressViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolver = new UniversalResolverService(web3.Eth, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS);
            var ensUtil = new EnsUtil();

            var name = "nick.eth";
            var dnsEncodedName = ensUtil.DnsEncode(name).HexToByteArray();
            var addrCallData = new AddrFunction { Node = ensUtil.GetNameHash(name).HexToByteArray() }.GetCallData();

            var result = await universalResolver.ResolveQueryAsync(dnsEncodedName, addrCallData).ConfigureAwait(false);
            var resolvedAddress = new AddrOutputDTO().DecodeOutput(result.ReturnValue1.ToHex()).ReturnValue1;

            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(resolvedAddress));
        }

        [Fact]
        public async void ShouldResolveIntegrationTestNameViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolver = new UniversalResolverService(web3.Eth, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS);
            var ensUtil = new EnsUtil();

            var name = "ur.integration-tests.eth";
            var dnsEncodedName = ensUtil.DnsEncode(name).HexToByteArray();
            var addrCallData = new AddrFunction { Node = ensUtil.GetNameHash(name).HexToByteArray() }.GetCallData();

            var result = await universalResolver.ResolveQueryAsync(dnsEncodedName, addrCallData).ConfigureAwait(false);
            var resolvedAddress = new AddrOutputDTO().DecodeOutput(result.ReturnValue1.ToHex()).ReturnValue1;

            var expectedAddress = "0x2222222222222222222222222222222222222222";
            Assert.True(expectedAddress.IsTheSameAddress(resolvedAddress));
        }

        [Fact]
        public async void ShouldResolveMulticallViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolver = new UniversalResolverService(web3.Eth, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS);
            var ensUtil = new EnsUtil();

            var name = "nick.eth";
            var node = ensUtil.GetNameHash(name).HexToByteArray();
            var dnsEncodedName = ensUtil.DnsEncode(name).HexToByteArray();

            var addrCall = new MulticallInputOutput<AddrFunction, AddrOutputDTO>(
                new AddrFunction { Node = node }, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS);
            var textCall = new MulticallInputOutput<TextFunction, TextOutputDTO>(
                new TextFunction { Node = node, Key = "url" }, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS);

            var multicallData = new MulticallFunction
            {
                Data = new List<byte[]> { addrCall.GetCallData(), textCall.GetCallData() }
            }.GetCallData();

            var result = await universalResolver.ResolveQueryAsync(dnsEncodedName, multicallData).ConfigureAwait(false);
            var innerResults = new MulticallOutputDTO().DecodeOutput(result.ReturnValue1.ToHex()).ReturnValue1;

            Assert.Equal(2, innerResults.Count);
            addrCall.Decode(innerResults[0]);
            textCall.Decode(innerResults[1]);

            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(addrCall.Output.ReturnValue1));

            var singleText = await universalResolver
                .ResolveQueryAsync(dnsEncodedName, new TextFunction { Node = node, Key = "url" }.GetCallData())
                .ConfigureAwait(false);
            var singleTextValue = new TextOutputDTO().DecodeOutput(singleText.ReturnValue1.ToHex()).ReturnValue1;
            Assert.Equal(singleTextValue, textCall.Output.ReturnValue1);
        }

        [Fact]
        public async void ShouldResolveAddressViaUniversalResolverServiceSeamless()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var address = await web3.Eth.GetEnsUniversalResolverService().ResolveAddressAsync("nick.eth").ConfigureAwait(false);
            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(address));
        }

        [Fact]
        public async void ShouldResolveRecordViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolverService = web3.Eth.GetEnsUniversalResolverService();

            var record = await universalResolverService
                .ResolveRecordAsync("nick.eth", TextDataKey.url, TextDataKey.avatar).ConfigureAwait(false);

            Assert.Equal("nick.eth", record.Name);
            var expectedAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedAddress.IsTheSameAddress(record.Address));

            var avatar = await universalResolverService.ResolveTextAsync("nick.eth", TextDataKey.avatar).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(avatar))
                Assert.Equal(avatar, record.Texts["avatar"]);
        }

        [Fact]
        public async void ShouldResolveMultipleRecordsViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolverService = web3.Eth.GetEnsUniversalResolverService();

            var records = await universalResolverService
                .ResolveRecordsAsync(new[] { "nick.eth", "nethereum.eth", "1.offchainexample.eth" }, TextDataKey.url).ConfigureAwait(false);

            Assert.Equal(3, records.Count);
            Assert.Equal("nick.eth", records[0].Name);
            Assert.Equal("nethereum.eth", records[1].Name);
            Assert.Equal("1.offchainexample.eth", records[2].Name);
            var expectedNickAddress = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expectedNickAddress.IsTheSameAddress(records[0].Address));
            var expectedOffchainAddress = "0x41563129cDbbD0c5D3e1c86cf9563926b243834d";
            Assert.True(expectedOffchainAddress.IsTheSameAddress(records[2].Address));
        }

        [Fact]
        public async void ShouldResolveTextsViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolverService = web3.Eth.GetEnsUniversalResolverService();

            var texts = await universalResolverService.ResolveTextsAsync("nick.eth", "url", "avatar").ConfigureAwait(false);

            Assert.True(texts.ContainsKey("url"));
            Assert.True(texts.ContainsKey("avatar"));

            var url = await universalResolverService.ResolveTextAsync("nick.eth", "url").ConfigureAwait(false);
            var avatar = await universalResolverService.ResolveTextAsync("nick.eth", "avatar").ConfigureAwait(false);
            Assert.Equal(url, texts["url"]);
            Assert.Equal(avatar, texts["avatar"]);
        }

        [Fact]
        public async void ShouldResolveAddressOfflineViaUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var universalResolverService = web3.Eth.GetEnsUniversalResolverService();
            var theAddress = await universalResolverService.ResolveAddressAsync("1.offchainexample.eth").ConfigureAwait(false);
            var expected = "0x41563129cDbbD0c5D3e1c86cf9563926b243834d";
            Assert.True(expected.IsTheSameAddress(theAddress));
        }

        [Fact]
        public async void ShouldDefaultEnsServiceToUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();

            Assert.NotNull(ensService.UniversalResolverService);

            var legacyEnsService = new ENSService(web3.Eth, universalResolverAddress: null);
            Assert.Null(legacyEnsService.UniversalResolverService);

            var address = await ensService.ResolveAddressAsync("nick.eth").ConfigureAwait(false);
            var expected = "0xb8c2C29ee19D8307cb7255e1Cd9CbDE883A267d5";
            Assert.True(expected.IsTheSameAddress(address));
        }

        [Fact]
        public async void ShouldResolveOffchainRecordViaLocalBatchGatewayUsingUniversalResolver()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var recordingCcip = new RecordingBatchGatewayCcipService();
            var universalResolverService = new ENSUniversalResolverService(
                web3.Eth, CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS, recordingCcip);

            var record = await universalResolverService
                .ResolveRecordAsync("1.offchainexample.eth", TextDataKey.url).ConfigureAwait(false);

            var expectedOffchainAddress = "0x41563129cDbbD0c5D3e1c86cf9563926b243834d";
            Assert.True(expectedOffchainAddress.IsTheSameAddress(record.Address));
            Assert.True(recordingCcip.LocalBatchGatewayUsed,
                "Expected the ENSIP-21 local batch gateway (x-batch-gateway) to be used for the offchain record resolution");
        }

        public class RecordingBatchGatewayCcipService : EnsCCIPService
        {
            public bool LocalBatchGatewayUsed { get; private set; }

            protected override Task<string> FetchGatewayDataAsync(
                Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition.OffchainLookupError offchainLookup)
            {
                if (offchainLookup.Urls != null && offchainLookup.Urls.Contains(LocalBatchGatewayUrl))
                {
                    LocalBatchGatewayUsed = true;
                }
                return base.FetchGatewayDataAsync(offchainLookup);
            }
        }

        [Fact]
        public async void ShouldReverseResolveAddressMatoken()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var ensService = web3.Eth.GetEnsService();
            var addressToResolve = "0x5A384227B65FA093DEC03Ec34e111Db80A040615";
            var reverse = await ensService.ReverseResolveAsync(addressToResolve);
            var address = await ensService.ResolveAddressAsync(reverse).ConfigureAwait(false);
            Assert.True(address.IsTheSameAddress(addressToResolve));
        }


    }
}
