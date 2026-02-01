using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class EIP7702TestFixture : IAsyncLifetime
    {
        public DevChainNode Node { get; private set; } = null!;

        public string SenderPrivateKey { get; } = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string SenderAddress { get; } = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        public BigInteger ChainId { get; } = 31337;
        public BigInteger InitialBalance { get; } = BigInteger.Parse("10000000000000000000000");

        private readonly Transaction7702Signer _signer7702 = new();

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            Node = new DevChainNode(config);
            await Node.StartAsync(new[] { SenderAddress }, InitialBalance);
        }

        public Task DisposeAsync()
        {
            Node?.Dispose();
            return Task.CompletedTask;
        }

        public (EthECKey key, string address) GenerateNewAuthority()
        {
            var key = EthECKey.GenerateKey();
            var address = key.GetPublicAddress();
            return (key, address);
        }

        public async Task FundAddressAsync(string address, BigInteger amount)
        {
            await Node.SetBalanceAsync(address, amount);
        }

        public ISignedTransaction CreateAndSignType4Transaction(
            BigInteger senderNonce,
            string receiverAddress,
            List<Authorisation7702Signed> authList,
            BigInteger? gasLimit = null,
            byte[] data = null)
        {
            var tx7702 = new Transaction7702(
                chainId: ChainId,
                nonce: senderNonce,
                maxPriorityFeePerGas: 1_000_000_000,
                maxFeePerGas: 2_000_000_000,
                gasLimit: gasLimit ?? 100_000,
                receiverAddress: receiverAddress,
                amount: 0,
                data: data?.ToHex() ?? "",
                accessList: null,
                authorisationList: authList);

            var signedTxHex = _signer7702.SignTransaction(SenderPrivateKey, tx7702);
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        public Authorisation7702Signed SignAuthorization(
            EthECKey authorityKey,
            string delegateAddress,
            BigInteger nonce,
            BigInteger? chainId = null)
        {
            var auth = new Authorisation7702
            {
                ChainId = chainId ?? ChainId,
                Address = delegateAddress,
                Nonce = nonce
            };
            var authSigner = new Authorisation7702Signer();
            return authSigner.SignAuthorisation(authorityKey, auth);
        }
    }
}
