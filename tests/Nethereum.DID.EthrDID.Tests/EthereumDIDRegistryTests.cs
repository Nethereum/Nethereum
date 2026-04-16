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
    public class EthereumDIDRegistryTests : IAsyncLifetime
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

        private DevChainNode _node;
        private IWeb3 _web3;
        private EthereumDIDRegistryService _service;
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
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void ShouldDeployRegistry()
        {
            Assert.NotNull(_service.ContractHandler);
        }

        [Fact]
        public async Task IdentityOwnerShouldReturnSelfByDefault()
        {
            var owner = await _service.IdentityOwnerQueryAsync(_address);
            Assert.Equal(_address.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task ShouldChangeOwner()
        {
            var newOwner = "0x1111111111111111111111111111111111111111";

            var receipt = await _service.ChangeOwnerRequestAndWaitForReceiptAsync(_address, _address, newOwner);
            Assert.NotNull(receipt);
            Assert.Equal(1uL, receipt.Status.Value);

            var owner = await _service.IdentityOwnerQueryAsync(_address);
            Assert.Equal(newOwner.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task ShouldAddAndValidateDelegate()
        {
            var delegateAddress = "0x2222222222222222222222222222222222222222";
            var delegateType = PadRight(Encoding.UTF8.GetBytes("veriKey"), 32);
            var validity = new BigInteger(86400);

            var receipt = await _service.AddDelegateRequestAndWaitForReceiptAsync(
                _address, delegateType, delegateAddress, validity);
            Assert.NotNull(receipt);
            Assert.Equal(1uL, receipt.Status.Value);

            var isValid = await _service.ValidDelegateQueryAsync(_address, delegateType, delegateAddress);
            Assert.True(isValid);
        }

        [Fact]
        public async Task ShouldSetAttribute()
        {
            var name = PadRight(Encoding.UTF8.GetBytes("did/svc/MessagingService"), 32);
            var value = Encoding.UTF8.GetBytes("https://messenger.example.com");
            var validity = new BigInteger(86400);

            var receipt = await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, value, validity);
            Assert.NotNull(receipt);
            Assert.Equal(1uL, receipt.Status.Value);

            var changed = await _service.ChangedQueryAsync(_address);
            Assert.True(changed > 0);
        }

        [Fact]
        public async Task ShouldRevokeDelegate()
        {
            var delegateAddress = "0x3333333333333333333333333333333333333333";
            var delegateType = PadRight(Encoding.UTF8.GetBytes("veriKey"), 32);

            await _service.AddDelegateRequestAndWaitForReceiptAsync(
                _address, delegateType, delegateAddress, new BigInteger(86400));
            var isValidBefore = await _service.ValidDelegateQueryAsync(
                _address, delegateType, delegateAddress);
            Assert.True(isValidBefore);

            await _service.RevokeDelegateRequestAndWaitForReceiptAsync(
                _address, delegateType, delegateAddress);
            var isValidAfter = await _service.ValidDelegateQueryAsync(
                _address, delegateType, delegateAddress);
            Assert.False(isValidAfter);
        }

        [Fact]
        public async Task ShouldRevokeAttribute()
        {
            var name = PadRight(Encoding.UTF8.GetBytes("did/svc/TestService"), 32);
            var value = Encoding.UTF8.GetBytes("https://test.example.com");

            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, value, new BigInteger(86400));
            var changedAfterSet = await _service.ChangedQueryAsync(_address);
            Assert.True(changedAfterSet > 0);

            var receipt = await _service.RevokeAttributeRequestAndWaitForReceiptAsync(
                _address, name, value);
            Assert.NotNull(receipt);
            Assert.Equal(1uL, receipt.Status.Value);
        }

        [Fact]
        public async Task ShouldTrackChangedBlockNumber()
        {
            var changedBefore = await _service.ChangedQueryAsync(_address);
            Assert.Equal(BigInteger.Zero, changedBefore);

            var name = PadRight(Encoding.UTF8.GetBytes("did/svc/Test"), 32);
            var value = Encoding.UTF8.GetBytes("https://test.com");
            await _service.SetAttributeRequestAndWaitForReceiptAsync(
                _address, name, value, new BigInteger(3600));

            var changedAfter = await _service.ChangedQueryAsync(_address);
            Assert.True(changedAfter > 0);
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
