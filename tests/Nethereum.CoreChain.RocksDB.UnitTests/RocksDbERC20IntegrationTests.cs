using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbERC20IntegrationTests : IAsyncLifetime
    {
        private RocksDbTestFixture _fixture = null!;
        private DevChainNode _node = null!;
        private readonly LegacyTransactionSigner _signer = new();

        public string PrivateKey { get; } = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string Address { get; } = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public string RecipientAddress { get; } = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        public BigInteger ChainId { get; } = 31337;
        public BigInteger InitialBalance { get; } = BigInteger.Parse("10000000000000000000000");

        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        public async Task InitializeAsync()
        {
            _fixture = new RocksDbTestFixture();
            _node = CreateNode();
            await _node.StartAsync(new[] { Address }, InitialBalance);
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        private DevChainNode CreateNode(DevChainConfig config = null)
        {
            return new DevChainNode(
                config ?? new DevChainConfig { ChainId = ChainId, AutoMine = true },
                _fixture.BlockStore,
                _fixture.TransactionStore,
                _fixture.ReceiptStore,
                _fixture.LogStore,
                _fixture.StateStore,
                _fixture.FilterStore,
                _fixture.TrieNodeStore);
        }

        private ISignedTransaction CreateContractDeploymentTransaction(byte[] bytecode, BigInteger? nonce = null)
        {
            var txNonce = nonce ?? _node.GetNonceAsync(Address).Result;
            BigInteger txGasPrice = 1_000_000_000;
            BigInteger txGasLimit = 3_000_000;

            var signedTxHex = _signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                "",
                BigInteger.Zero,
                txNonce,
                txGasPrice,
                txGasLimit,
                bytecode.ToHex());

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private ISignedTransaction CreateSignedTransaction(string to, BigInteger value, byte[] data = null, BigInteger? nonce = null)
        {
            var txNonce = nonce ?? _node.GetNonceAsync(Address).Result;
            BigInteger txGasPrice = 1_000_000_000;
            BigInteger txGasLimit = data != null ? 500_000 : 21_000;

            var signedTxHex = _signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                to,
                value,
                txNonce,
                txGasPrice,
                txGasLimit,
                data?.ToHex() ?? "");

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        [Fact]
        public async Task DeployERC20_PersistsContractCode()
        {
            var bytecode = ERC20TestContract.GetDeploymentBytecode();
            var signedTx = CreateContractDeploymentTransaction(bytecode);
            var result = await _node.SendTransactionAsync(signedTx);

            Assert.True(result.Success, $"Deployment failed: {result.RevertReason}");

            var receiptInfo = await _node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            var contractAddress = receiptInfo.ContractAddress;

            Assert.NotNull(contractAddress);

            var code = await _node.GetCodeAsync(contractAddress);
            Assert.NotNull(code);
            Assert.True(code.Length > 0, "Contract code should be persisted");
        }

        [Fact]
        public async Task DeployAndMint_PersistsBalanceInStorage()
        {
            var contractAddress = await DeployERC20Async();
            var mintAmount = OneToken * 1000;

            await MintERC20Async(contractAddress, Address, mintAmount);

            var balance = await GetERC20BalanceAsync(contractAddress, Address);
            Assert.Equal(mintAmount, balance);
        }

        [Fact]
        public async Task Transfer_UpdatesStorageCorrectly()
        {
            var contractAddress = await DeployERC20Async();
            var initialBalance = OneToken * 1000;
            var transferAmount = OneToken * 100;

            await MintERC20Async(contractAddress, Address, initialBalance);

            var transferResult = await TransferERC20Async(contractAddress, RecipientAddress, transferAmount);
            Assert.True(transferResult.Success, $"Transfer failed: {transferResult.RevertReason}");

            var senderBalance = await GetERC20BalanceAsync(contractAddress, Address);
            var recipientBalance = await GetERC20BalanceAsync(contractAddress, RecipientAddress);

            Assert.Equal(initialBalance - transferAmount, senderBalance);
            Assert.Equal(transferAmount, recipientBalance);
        }

        [Fact]
        public async Task ContractState_SurvivesManagerRecreate()
        {
            var dbPath = _fixture.DatabasePath;
            var contractAddress = await DeployERC20Async();
            var mintAmount = OneToken * 500;

            await MintERC20Async(contractAddress, Address, mintAmount);

            var initialBalance = await GetERC20BalanceAsync(contractAddress, Address);
            Assert.Equal(mintAmount, initialBalance);

            _fixture.Manager.Dispose();

            var options = new RocksDbStorageOptions { DatabasePath = dbPath };
            using var newManager = new RocksDbManager(options);
            var newBlockStore = new Stores.RocksDbBlockStore(newManager);
            var newStateStore = new Stores.RocksDbStateStore(newManager);
            var newTransactionStore = new Stores.RocksDbTransactionStore(newManager, newBlockStore);
            var newReceiptStore = new Stores.RocksDbReceiptStore(newManager, newBlockStore);
            var newLogStore = new Stores.RocksDbLogStore(newManager);
            var newFilterStore = new Stores.RocksDbFilterStore(newManager);
            var newTrieNodeStore = new Stores.RocksDbTrieNodeStore(newManager);

            var newNode = new DevChainNode(
                new DevChainConfig { ChainId = ChainId, AutoMine = true },
                newBlockStore,
                newTransactionStore,
                newReceiptStore,
                newLogStore,
                newStateStore,
                newFilterStore,
                newTrieNodeStore);

            await newNode.StartAsync();

            var code = await newNode.GetCodeAsync(contractAddress);
            Assert.NotNull(code);
            Assert.True(code.Length > 0, "Contract code should survive manager recreate");

            var balanceFunction = new BalanceOfFunction { Account = Address };
            var callData = balanceFunction.GetCallData();
            var callResult = await newNode.CallAsync(contractAddress, callData);
            Assert.True(callResult.Success, $"Call failed: {callResult.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<BalanceOfOutputDTO>(callResult.ReturnData.ToHex(true));
            Assert.Equal(mintAmount, output.ReturnValue1);
        }

        [Fact]
        public async Task MultipleTransactions_AllPersisted()
        {
            var contractAddress = await DeployERC20Async();
            var mintAmount = OneToken * 1000;

            await MintERC20Async(contractAddress, Address, mintAmount);

            var recipient1 = "0x1111111111111111111111111111111111111111";
            var recipient2 = "0x2222222222222222222222222222222222222222";

            await TransferERC20Async(contractAddress, recipient1, OneToken * 100);
            await TransferERC20Async(contractAddress, recipient2, OneToken * 200);
            await TransferERC20Async(contractAddress, recipient1, OneToken * 50);

            var senderBalance = await GetERC20BalanceAsync(contractAddress, Address);
            var balance1 = await GetERC20BalanceAsync(contractAddress, recipient1);
            var balance2 = await GetERC20BalanceAsync(contractAddress, recipient2);

            Assert.Equal(mintAmount - (OneToken * 350), senderBalance);
            Assert.Equal(OneToken * 150, balance1);
            Assert.Equal(OneToken * 200, balance2);
        }

        [Fact]
        public async Task TransactionReceipts_PersistedWithLogs()
        {
            var contractAddress = await DeployERC20Async();
            var mintAmount = OneToken * 100;

            var result = await MintERC20Async(contractAddress, Address, mintAmount);
            Assert.True(result.Success);
            Assert.NotNull(result.Logs);
            Assert.True(result.Logs.Count > 0, "Mint should emit Transfer event");

            var receipt = await _node.GetTransactionReceiptAsync(result.TransactionHash);
            Assert.NotNull(receipt);
            Assert.True(receipt.HasSucceeded);
        }

        [Fact]
        public async Task BlocksAndTransactions_PersistedCorrectly()
        {
            var contractAddress = await DeployERC20Async();
            await MintERC20Async(contractAddress, Address, OneToken * 100);
            await TransferERC20Async(contractAddress, RecipientAddress, OneToken * 50);

            var blockNumber = await _node.GetBlockNumberAsync();
            Assert.True(blockNumber >= 3, $"Expected at least 3 blocks, got {blockNumber}");

            for (int i = 0; i <= (int)blockNumber; i++)
            {
                var block = await _node.GetBlockByNumberAsync(i);
                Assert.NotNull(block);
                Assert.Equal(i, block.BlockNumber);
            }
        }

        [Fact]
        public async Task TotalSupply_TrackedCorrectly()
        {
            var contractAddress = await DeployERC20Async();

            var initialSupply = await GetERC20TotalSupplyAsync(contractAddress);
            Assert.Equal(BigInteger.Zero, initialSupply);

            await MintERC20Async(contractAddress, Address, OneToken * 500);

            var afterMint = await GetERC20TotalSupplyAsync(contractAddress);
            Assert.Equal(OneToken * 500, afterMint);

            await MintERC20Async(contractAddress, RecipientAddress, OneToken * 300);

            var finalSupply = await GetERC20TotalSupplyAsync(contractAddress);
            Assert.Equal(OneToken * 800, finalSupply);
        }

        private async Task<string> DeployERC20Async()
        {
            var bytecode = ERC20TestContract.GetDeploymentBytecode();
            var signedTx = CreateContractDeploymentTransaction(bytecode);
            var result = await _node.SendTransactionAsync(signedTx);

            if (!result.Success)
                throw new Exception($"ERC20 deployment failed: {result.RevertReason}");

            var receiptInfo = await _node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            return receiptInfo.ContractAddress;
        }

        private async Task<TransactionExecutionResult> MintERC20Async(string contractAddress, string to, BigInteger amount)
        {
            var mintFunction = new MintFunction { To = to, Amount = amount };
            var callData = mintFunction.GetCallData();
            var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
            return await _node.SendTransactionAsync(signedTx);
        }

        private async Task<TransactionExecutionResult> TransferERC20Async(string contractAddress, string to, BigInteger amount)
        {
            var transferFunction = new TransferFunction { To = to, Value = amount };
            var callData = transferFunction.GetCallData();
            var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
            return await _node.SendTransactionAsync(signedTx);
        }

        private async Task<BigInteger> GetERC20BalanceAsync(string contractAddress, string owner)
        {
            var balanceOfFunction = new BalanceOfFunction { Account = owner };
            var callData = balanceOfFunction.GetCallData();
            var result = await _node.CallAsync(contractAddress, callData);

            if (!result.Success)
                throw new Exception($"balanceOf call failed: {result.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<BalanceOfOutputDTO>(result.ReturnData.ToHex(true));
            return output.ReturnValue1;
        }

        private async Task<BigInteger> GetERC20TotalSupplyAsync(string contractAddress)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
            var callData = totalSupplyFunction.GetCallData();
            var result = await _node.CallAsync(contractAddress, callData);

            if (!result.Success)
                throw new Exception($"totalSupply call failed: {result.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<TotalSupplyOutputDTO>(result.ReturnData.ToHex(true));
            return output.ReturnValue1;
        }
    }

    public static class ERC20TestContract
    {
        public const string BYTECODE = "0x608060405234801561000f575f5ffd5b506040518060400160405280600a81526020016926b7b1b5902a37b5b2b760b11b815250604051806040016040528060048152602001634d4f434b60e01b815250816003908161005f919061010c565b50600461006c828261010c565b5050506101c6565b634e487b7160e01b5f52604160045260245ffd5b600181811c9082168061009c57607f821691505b6020821081036100ba57634e487b7160e01b5f52602260045260245ffd5b50919050565b601f82111561010757805f5260205f20601f840160051c810160208510156100e55750805b601f840160051c820191505b81811015610104575f81556001016100f1565b50505b505050565b81516001600160401b0381111561012557610125610074565b610139816101338454610088565b846100c0565b6020601f82116001811461016b575f83156101545750848201515b5f19600385901b1c1916600184901b178455610104565b5f84815260208120601f198516915b8281101561019a578785015182556020948501946001909201910161017a565b50848210156101b757868401515f19600387901b60f8161c191681555b50505050600190811b01905550565b610746806101d35f395ff3fe608060405234801561000f575f5ffd5b506004361061009b575f3560e01c806340c10f191161006357806340c10f191461011457806370a082311461012957806395d89b4114610151578063a9059cbb14610159578063dd62ed3e1461016c575f5ffd5b806306fdde031461009f578063095ea7b3146100bd57806318160ddd146100e057806323b872dd146100f2578063313ce56714610105575b5f5ffd5b6100a76101a4565b6040516100b491906105b6565b60405180910390f35b6100d06100cb366004610606565b610234565b60405190151581526020016100b4565b6002545b6040519081526020016100b4565b6100d061010036600461062e565b61024d565b604051601281526020016100b4565b610127610122366004610606565b610270565b005b6100e4610137366004610668565b6001600160a01b03165f9081526020819052604090205490565b6100a761027e565b6100d0610167366004610606565b61028d565b6100e461017a366004610688565b6001600160a01b039182165f90815260016020908152604080832093909416825291909152205490565b6060600380546101b3906106b9565b80601f01602080910402602001604051908101604052809291908181526020018280546101df906106b9565b801561022a5780601f106102015761010080835404028352916020019161022a565b820191905f5260205f20905b81548152906001019060200180831161020d57829003601f168201915b5050505050905090565b5f3361024181858561029a565b60019150505b92915050565b5f3361025a8582856102ac565b61026585858561032d565b506001949350505050565b61027a828261038a565b5050565b6060600480546101b3906106b9565b5f3361024181858561032d565b6102a783838360016103be565b505050565b6001600160a01b038381165f908152600160209081526040808320938616835292905220545f19811015610327578181101561031957604051637dc7a0d960e11b81526001600160a01b038416600482015260248101829052604481018390526064015b60405180910390fd5b61032784848484035f6103be565b50505050565b6001600160a01b03831661035657604051634b637e8f60e11b81525f6004820152602401610310565b6001600160a01b03821661037f5760405163ec442f0560e01b81525f6004820152602401610310565b6102a7838383610490565b6001600160a01b0382166103b35760405163ec442f0560e01b81525f6004820152602401610310565b61027a5f8383610490565b6001600160a01b0384166103e75760405163e602df0560e01b81525f6004820152602401610310565b6001600160a01b03831661041057604051634a1406b160e11b81525f6004820152602401610310565b6001600160a01b038085165f908152600160209081526040808320938716835292905220829055801561032757826001600160a01b0316846001600160a01b03167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9258460405161048291815260200190565b60405180910390a350505050565b6001600160a01b0383166104ba578060025f8282546104af91906106f1565b9091555061052a9050565b6001600160a01b0383165f908152602081905260409020548181101561050c5760405163391434e360e21b81526001600160a01b03851660048201526024810182905260448101839052606401610310565b6001600160a01b0384165f9081526020819052604090209082900390555b6001600160a01b03821661054657600280548290039055610564565b6001600160a01b0382165f9081526020819052604090208054820190555b816001600160a01b0316836001600160a01b03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef836040516105a991815260200190565b60405180910390a3505050565b602081525f82518060208401528060208501604085015e5f604082850101526040601f19601f83011684010191505092915050565b80356001600160a01b0381168114610601575f5ffd5b919050565b5f5f60408385031215610617575f5ffd5b610620836105eb565b946020939093013593505050565b5f5f5f60608486031215610640575f5ffd5b610649846105eb565b9250610657602085016105eb565b929592945050506040919091013590565b5f60208284031215610678575f5ffd5b610681826105eb565b9392505050565b5f5f60408385031215610699575f5ffd5b6106a2836105eb565b91506106b0602084016105eb565b90509250929050565b600181811c908216806106cd57607f821691505b6020821081036106eb57634e487b7160e01b5f52602260045260245ffd5b50919050565b8082018082111561024757634e487b7160e01b5f52601160045260245ffdfea2646970667358221220f151a46618647e457760c7d59f88e69de993a1735680605d19409339237705ed64736f6c634300081c0033";

        public static byte[] GetDeploymentBytecode()
        {
            return Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(BYTECODE);
        }
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    [FunctionOutput]
    public class BalanceOfOutputDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunction : FunctionMessage
    {
    }

    [FunctionOutput]
    public class TotalSupplyOutputDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    [Function("transfer", "bool")]
    public class TransferFunction : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    [Function("mint")]
    public class MintFunction : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }
}
