using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.DID.EthrDID.EthereumDIDRegistry;
using Nethereum.DID.EthrDID.EthereumDIDRegistry.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.DID.EthrDID.Tests
{
    public class EthrDidResolverTests : IAsyncLifetime
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

        private DevChainNode _node;
        private IWeb3 _web3;
        private EthereumDIDRegistryService _service;
        private EthrDidResolver _resolver;
        private string _address;

        public async Task InitializeAsync()
        {
            var account = new Account(PrivateKey);
            _address = account.Address;

            _node = new DevChainNode();
            await _node.StartAsync(new[] { _address });

            _web3 = _node.CreateWeb3(account);

            var deployment = new EthereumDIDRegistryDeployment();
            _service = await EthereumDIDRegistryService.DeployContractAndGetServiceAsync(
                _web3, deployment).ConfigureAwait(false);

            _resolver = new EthrDidResolver(_web3, _service.ContractHandler.ContractAddress);
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task ShouldResolveDefaultDid()
        {
            var did = "did:ethr:" + _address;

            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc);
            Assert.Equal(did, doc.Id);
            Assert.NotNull(doc.VerificationMethod);
            Assert.True(doc.VerificationMethod.Count >= 1);
            Assert.Equal(DidConstants.EcdsaSecp256k1RecoveryMethod2020, doc.VerificationMethod[0].Type);
            Assert.NotNull(doc.VerificationMethod[0].BlockchainAccountId);
            Assert.Contains(_address.ToLower(), doc.VerificationMethod[0].BlockchainAccountId.ToLower());
        }

        [Fact]
        public async Task ShouldResolveDefaultDidWithAuthentication()
        {
            var did = "did:ethr:" + _address;
            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc.Authentication);
            Assert.True(doc.Authentication.Count >= 1);
            Assert.True(doc.Authentication[0].IsReference);
            Assert.Equal(did + "#controller", doc.Authentication[0].VerificationMethodReference);
        }

        [Fact]
        public async Task ShouldResolveDefaultDidWithAssertionMethod()
        {
            var did = "did:ethr:" + _address;
            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc.AssertionMethod);
            Assert.True(doc.AssertionMethod.Count >= 1);
        }

        [Fact]
        public async Task ShouldResolveDefaultDidWithCorrectContext()
        {
            var did = "did:ethr:" + _address;
            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc.Context);
            Assert.Contains(DidConstants.DidContextV1, doc.Context);
        }

        [Fact]
        public async Task ShouldResolveDidWithDelegate()
        {
            var did = "did:ethr:" + _address;
            var delegateAddress = "0x4444444444444444444444444444444444444444";
            var delegateType = PadRight(Encoding.UTF8.GetBytes("veriKey"), 32);

            await _service.AddDelegateRequestAndWaitForReceiptAsync(
                _address, delegateType, delegateAddress, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            Assert.True(doc.VerificationMethod.Count >= 2);
            var delegateVm = doc.VerificationMethod[1];
            Assert.Contains(delegateAddress.ToLower(), delegateVm.BlockchainAccountId.ToLower());
        }

        [Fact]
        public async Task ShouldResolveDidWithSignAuthDelegate()
        {
            var did = "did:ethr:" + _address;
            var delegateAddress = "0x5555555555555555555555555555555555555555";
            var delegateType = PadRight(Encoding.UTF8.GetBytes("sigAuth"), 32);

            await _service.AddDelegateRequestAndWaitForReceiptAsync(
                _address, delegateType, delegateAddress, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            Assert.True(doc.Authentication.Count >= 2);
        }

        [Fact]
        public async Task ShouldResolveDidWithServiceEndpoint()
        {
            var did = "did:ethr:" + _address;
            var name = PadRight(Encoding.UTF8.GetBytes("did/svc/MessagingService"), 32);
            var value = Encoding.UTF8.GetBytes("https://messenger.example.com");

            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, value, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc.Service);
            Assert.True(doc.Service.Count >= 1);
            Assert.Equal("MessagingService", doc.Service[0].Type);
            Assert.Equal("https://messenger.example.com", doc.Service[0].ServiceEndpoint);
        }

        [Fact]
        public async Task ShouldResolveDidWithMultipleServices()
        {
            var did = "did:ethr:" + _address;

            var name1 = PadRight(Encoding.UTF8.GetBytes("did/svc/Messaging"), 32);
            var value1 = Encoding.UTF8.GetBytes("https://msg.example.com");
            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name1, value1, new BigInteger(86400));

            var name2 = PadRight(Encoding.UTF8.GetBytes("did/svc/Profile"), 32);
            var value2 = Encoding.UTF8.GetBytes("https://profile.example.com");
            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name2, value2, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            Assert.NotNull(doc.Service);
            Assert.Equal(2, doc.Service.Count);
        }

        [Fact]
        public async Task ShouldResolveDidWithChangedOwner()
        {
            var did = "did:ethr:" + _address;
            var newOwner = "0x6666666666666666666666666666666666666666";

            await _service.ChangeOwnerRequestAndWaitForReceiptAsync(_address, _address, newOwner);

            var doc = await _resolver.ResolveAsync(did);

            Assert.Contains(newOwner.ToLower(), doc.Controller[0].ToLower());
            Assert.Contains(newOwner.ToLower(), doc.VerificationMethod[0].BlockchainAccountId.ToLower());
        }

        [Fact]
        public async Task ShouldResolveDidWithPublicKeyAttribute()
        {
            var did = "did:ethr:" + _address;
            var name = PadRight(Encoding.UTF8.GetBytes("did/pub/Secp256k1/veriKey/hex"), 32);
            var pubKeyBytes = new byte[] { 0x04, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67 };

            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, pubKeyBytes, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            Assert.True(doc.VerificationMethod.Count >= 2);
            var keyVm = doc.VerificationMethod[doc.VerificationMethod.Count - 1];
            Assert.Equal(DidConstants.EcdsaSecp256k1VerificationKey2019, keyVm.Type);
            Assert.NotNull(keyVm.PublicKeyHex);
        }

        [Fact]
        public async Task ShouldResolveDidWithNoChangesReturnsNoServices()
        {
            var did = "did:ethr:" + _address;
            var doc = await _resolver.ResolveAsync(did);

            Assert.Null(doc.Service);
        }

        [Fact]
        public async Task ShouldSerializeResolvedDocumentToJson()
        {
            var did = "did:ethr:" + _address;
            var name = PadRight(Encoding.UTF8.GetBytes("did/svc/Test"), 32);
            var value = Encoding.UTF8.GetBytes("https://test.com");
            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, value, new BigInteger(86400));

            var doc = await _resolver.ResolveAsync(did);

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(doc,
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    Formatting = Newtonsoft.Json.Formatting.Indented
                });

            Assert.Contains("\"@context\"", json);
            Assert.Contains("\"id\"", json);
            Assert.Contains("\"verificationMethod\"", json);
            Assert.Contains(did, json);

            var reparsed = Newtonsoft.Json.JsonConvert.DeserializeObject<DidDocument>(json);
            Assert.Equal(doc.Id, reparsed.Id);
            Assert.Equal(doc.VerificationMethod.Count, reparsed.VerificationMethod.Count);
        }

        private static byte[] PadRight(byte[] bytes, int totalLength)
        {
            if (bytes.Length >= totalLength) return bytes;
            var padded = new byte[totalLength];
            Array.Copy(bytes, padded, bytes.Length);
            return padded;
        }
    }
}
