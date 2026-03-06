using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using IAppChain = Nethereum.AppChain.IAppChain;
using AppChainConfig = Nethereum.AppChain.AppChainConfig;
using GenesisOptions = Nethereum.AppChain.GenesisOptions;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class StubAppChain : IAppChain
    {
        public AppChainConfig Config { get; } = AppChainConfig.Default;
        public IBlockStore Blocks => throw new NotImplementedException();
        public ITransactionStore Transactions => throw new NotImplementedException();
        public IReceiptStore Receipts => throw new NotImplementedException();
        public ILogStore Logs => throw new NotImplementedException();
        public IStateStore State => throw new NotImplementedException();
        public ITrieNodeStore TrieNodes => throw new NotImplementedException();
        public string WorldAddress => Config.WorldAddress;
        public string Create2FactoryAddress => Genesis.Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task InitializeAsync(GenesisOptions options) => Task.CompletedTask;
        public Task ApplyGenesisStateAsync(GenesisOptions options) => Task.CompletedTask;
        public Task<BigInteger> GetBlockNumberAsync() => Task.FromResult(BigInteger.Zero);
        public Task<BlockHeader?> GetLatestBlockAsync() => Task.FromResult<BlockHeader?>(null);
        public Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber) => Task.FromResult<BlockHeader?>(null);
        public Task<BlockHeader?> GetBlockByHashAsync(byte[] blockHash) => Task.FromResult<BlockHeader?>(null);
        public Task<BigInteger> GetBalanceAsync(string address) => Task.FromResult(BigInteger.Zero);
        public Task<BigInteger> GetNonceAsync(string address) => Task.FromResult(BigInteger.Zero);
        public Task<byte[]?> GetCodeAsync(string address) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> GetStorageAtAsync(string address, BigInteger slot) => Task.FromResult<byte[]?>(null);
        public Task<Account?> GetAccountAsync(string address) => Task.FromResult<Account?>(null);
        public Task<ISignedTransaction?> GetTransactionByHashAsync(byte[] txHash) => Task.FromResult<ISignedTransaction?>(null);
        public Task<Receipt?> GetTransactionReceiptAsync(byte[] txHash) => Task.FromResult<Receipt?>(null);
    }

    public class PolicyEnforcerTests
    {
        private readonly IAppChain _stubAppChain;
        private readonly string _testPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _testAddress;

        public PolicyEnforcerTests()
        {
            _stubAppChain = new StubAppChain();
            var key = new EthECKey(_testPrivateKey);
            _testAddress = key.GetPublicAddress();
        }

        [Fact]
        public async Task PolicyEnforcer_DisabledPolicy_AllowsAll()
        {
            var policy = new PolicyConfig { Enabled = false };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var tx = CreateSignedTransaction();
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task PolicyEnforcer_EnabledWithEmptyAllowlist_AllowsAll()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                AllowedWriters = new List<string>()
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var tx = CreateSignedTransaction();
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task PolicyEnforcer_AllowlistWithSender_Allows()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                AllowedWriters = new List<string> { _testAddress }
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var tx = CreateSignedTransaction();
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task PolicyEnforcer_AllowlistWithoutSender_Rejects()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                AllowedWriters = new List<string> { "0x0000000000000000000000000000000000000001" }
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var tx = CreateSignedTransaction();
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.False(result.IsValid);
            Assert.Equal(PolicyViolationType.UnauthorizedSender, result.ViolationType);
        }

        [Fact]
        public async Task PolicyEnforcer_CalldataExceedsLimit_Rejects()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 100
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var largeData = new byte[1000];
            var tx = CreateSignedTransaction(data: largeData);
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.False(result.IsValid);
            Assert.Equal(PolicyViolationType.CalldataTooLarge, result.ViolationType);
        }

        [Fact]
        public async Task PolicyEnforcer_CalldataWithinLimit_Allows()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 10000
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var smallData = new byte[100];
            var tx = CreateSignedTransaction(data: smallData);
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void PolicyEnforcer_UpdatePolicy_UpdatesRules()
        {
            var initialPolicy = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 1000
            };
            var enforcer = new PolicyEnforcer(initialPolicy, _stubAppChain);

            var newPolicy = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 2000
            };
            enforcer.UpdatePolicy(newPolicy);

            Assert.Equal(2000, enforcer.Policy.MaxCalldataBytes);
        }

        [Fact]
        public void PolicyEnforcer_UpdateWritersRoot_UpdatesRoot()
        {
            var policy = new PolicyConfig { Enabled = true };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var newRoot = new byte[] { 1, 2, 3, 4 };
            enforcer.UpdateWritersRoot(newRoot);

            Assert.Equal(newRoot, enforcer.Policy.WritersRoot);
        }

        [Fact]
        public async Task PolicyEnforcer_AllowlistCaseInsensitive_Works()
        {
            var policy = new PolicyConfig
            {
                Enabled = true,
                AllowedWriters = new List<string> { _testAddress.ToUpperInvariant() }
            };
            var enforcer = new PolicyEnforcer(policy, _stubAppChain);

            var tx = CreateSignedTransaction();
            var result = await enforcer.ValidateTransactionAsync(tx);

            Assert.True(result.IsValid);
        }

        private ISignedTransaction CreateSignedTransaction(byte[]? data = null, int nonce = 0)
        {
            var privateKey = new EthECKey(_testPrivateKey);
            var dataHex = data != null ? "0x" + BitConverter.ToString(data).Replace("-", "").ToLowerInvariant() : null;

            var transaction = new Transaction1559(
                chainId: new BigInteger(1),
                nonce: new BigInteger(nonce),
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: BigInteger.Zero,
                data: dataHex,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }
}
