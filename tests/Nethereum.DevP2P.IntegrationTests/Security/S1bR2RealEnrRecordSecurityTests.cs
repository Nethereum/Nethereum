using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.Enr;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2RealEnrRecordSecurityTests
    {
        private const string Eip778Example = "enr:-IS4QHCYrYZbAKWCBRlAy5zzaDZXJBGkcnh4MHcBFZntXNFrdvJjX04jRzjzCBOonrkTfj499SZuOh8R33Ls8RRcy5wBgmlkgnY0gmlwhH8AAAGJc2VjcDI1NmsxoQPKY0yuDUmstAHYpMa2_oxVtw0RW_QAdpzBQA8yWM0xOIN1ZHCCdl8";
        private const string GethBootnodeIp4 = "enr:-Ku4QImhMc1z8yCiNJ1TyUxdcfNucje3BGwEHzodEZUan8PherEo4sF7pPHPSIB1NNuSg5fZy7qFsjmUKs2ea1Whi0EBh2F0dG5ldHOIAAAAAAAAAACEZXRoMpD1pf1CAAAAAP__________gmlkgnY0gmlwhBLf22SJc2VjcDI1NmsxoQOVphkDqal4QzPMksc5wnpuC3gvSC8AfbFOnZY_On34wIN1ZHCCIyg";
        private const string GethBootnodeDualStack = "enr:-Le4QPUXJS2BTORXxyx2Ia-9ae4YqA_JWX3ssj4E_J-3z1A-HmFGrU8BpvpqhNabayXeOZ2Nq_sbeDgtzMJpLLnXFgAChGV0aDKQtTA_KgEAAAAAIgEAAAAAAIJpZIJ2NIJpcISsaa0Zg2lwNpAkAIkHAAAAAPA8kv_-awoTiXNlY3AyNTZrMaEDHAD2JKYevx89W0CcFJFiskdcEzkH_Wdv9iW42qLK79ODdWRwgiMohHVkcDaCI4I";

        private const string MainnetEthRecord = "enr:-J24QMWIHblfE0mLZhHGQKP-srf27LBFUUNddMf9pESDUTHhZdUyN6MdEDy9PD-orLBX0bJefYHxE5BGwg1Rw1iYTTeGAZ_rZ8Wrg2V0aMfGhAfJRi6AgmlkgnY0gmlwhMugWpqJc2VjcDI1NmsxoQLFg-wSNTKCBe-BA6wa-0Lxw7Q5t352benZSlGjZo6zRoN0Y3CCdl-DdWRwgnZf";

        [Theory]
        [Trait("Category", "Security")]
        [InlineData(Eip778Example)]
        [InlineData(GethBootnodeIp4)]
        [InlineData(GethBootnodeDualStack)]
        public void Given_RealCapturedEnr_When_DecodedVerifiedAndReEncoded_Then_SignatureVerifiesAndBytesAreIdentical(string url)
        {
            var record = EnrRecordEncoder.ParseUrl(url);

            Assert.True(EnrRecordSigner.Verify(record));
            Assert.Equal(url, EnrRecordEncoder.ToUrl(record));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RealMainnetEnrWithAnEthList_When_DecodedVerifiedAndReEncoded_Then_ItVerifiesKeepsTheListAndRoundTrips()
        {
            var record = EnrRecordEncoder.ParseUrl(MainnetEthRecord);

            Assert.True(EnrRecordSigner.Verify(record));
            Assert.True(record.IsListValue("eth"));
            Assert.Equal(MainnetEthRecord, EnrRecordEncoder.ToUrl(record));
            Assert.Equal(IPAddress.Parse("203.160.90.154"), record.IP4);
            Assert.Equal((ushort)30303, record.TcpPort);
            Assert.Equal((ushort)30303, record.UdpPort);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_DnsTreeWhoseLeafIsTheRealMainnetEnr_When_Resolved_Then_ItYieldsOneEnode()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            tree.SetRoot(tree.AddBranch(tree.Add(MainnetEthRecord)), tree.AddBranch());
            var resolver = new Nethereum.DevP2P.Dns.EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 10, CancellationToken.None);

            var enr = Assert.Single(enrs);
            var enode = Nethereum.DevP2P.Discv5.Discv5PeerDiscoveryService.ConvertEnrToEnode(enr);
            Assert.NotNull(enode);
            Assert.Contains("203.160.90.154:30303", enode);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Eip778ExampleRecord_When_Decoded_Then_SequenceAddressAndPortAreRead()
        {
            var record = EnrRecordEncoder.ParseUrl(Eip778Example);

            Assert.Equal(1UL, record.Sequence);
            Assert.Equal(IPAddress.Loopback, record.IP4);
            Assert.Equal((ushort)30303, record.UdpPort);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_GethBootnodeRecord_When_Decoded_Then_AddressAndPortsAreRead()
        {
            var record = EnrRecordEncoder.ParseUrl(GethBootnodeDualStack);

            Assert.Equal(IPAddress.Parse("172.105.173.25"), record.IP4);
            Assert.NotNull(record.IP6);
            Assert.Equal((ushort)9000, record.UdpPort);
            Assert.Equal((ushort)9090, record.UdpPort6);
        }
    }
}
