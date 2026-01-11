using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class DevChainNodeFixture : IAsyncLifetime
    {
        public DevChainNode Node { get; private set; } = null!;

        public string PrivateKey { get; } = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string Address { get; } = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        public string PrivateKey2 { get; } = "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        public string Address2 { get; } = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        public BigInteger ChainId { get; } = 31337;
        public BigInteger InitialBalance { get; } = BigInteger.Parse("10000000000000000000000"); // 10000 ETH

        public string RecipientAddress { get; } = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        private readonly LegacyTransactionSigner _signer = new();
        private readonly LegacyTransactionSigner _signer2 = new();

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            Node = new DevChainNode(config);
            await Node.StartAsync(new[] { Address, Address2 }, InitialBalance);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public ISignedTransaction CreateSignedTransaction(
            string to,
            BigInteger value,
            byte[]? data = null,
            BigInteger? gasLimit = null,
            BigInteger? gasPrice = null,
            BigInteger? nonce = null)
        {
            var txNonce = nonce ?? Node.GetNonceAsync(Address).Result;
            var txGasPrice = gasPrice ?? 1_000_000_000; // 1 gwei
            var txGasLimit = gasLimit ?? (data != null ? 500_000 : 21_000);

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

        public ISignedTransaction CreateContractDeploymentTransaction(
            byte[] bytecode,
            BigInteger? gasLimit = null,
            BigInteger? nonce = null)
        {
            var txNonce = nonce ?? Node.GetNonceAsync(Address).Result;
            var txGasLimit = gasLimit ?? 3_000_000;
            BigInteger txGasPrice = 1_000_000_000; // 1 gwei

            var signedTxHex = _signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                "", // empty to address for contract deployment
                BigInteger.Zero,
                txNonce,
                txGasPrice,
                txGasLimit,
                bytecode.ToHex());

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        public async Task<string> DeployERC20Async(BigInteger? initialMintAmount = null)
        {
            var bytecode = ERC20Contract.GetDeploymentBytecode();
            var signedTx = CreateContractDeploymentTransaction(bytecode);
            var result = await Node.SendTransactionAsync(signedTx);

            if (!result.Success)
                throw new Exception($"ERC20 deployment failed: {result.RevertReason}");

            var receiptInfo = await Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            var contractAddress = receiptInfo.ContractAddress;

            if (initialMintAmount.HasValue && initialMintAmount.Value > 0)
            {
                await MintERC20Async(contractAddress, Address, initialMintAmount.Value);
            }

            return contractAddress;
        }

        public async Task<TransactionExecutionResult> MintERC20Async(string contractAddress, string to, BigInteger amount)
        {
            var mintFunction = new MintFunction { To = to, Amount = amount };
            var callData = mintFunction.GetCallData();
            var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
            return await Node.SendTransactionAsync(signedTx);
        }

        public async Task<BigInteger> GetERC20BalanceAsync(string contractAddress, string owner)
        {
            var balanceOfFunction = new BalanceOfFunction { Account = owner };
            var callData = balanceOfFunction.GetCallData();
            var result = await Node.CallAsync(contractAddress, callData);

            if (!result.Success)
                throw new Exception($"balanceOf call failed: {result.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<BalanceOfOutputDTO>(result.ReturnData.ToHex(true));
            return output.ReturnValue1;
        }

        public async Task<BigInteger> GetERC20TotalSupplyAsync(string contractAddress)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
            var callData = totalSupplyFunction.GetCallData();
            var result = await Node.CallAsync(contractAddress, callData);

            if (!result.Success)
                throw new Exception($"totalSupply call failed: {result.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<TotalSupplyOutputDTO>(result.ReturnData.ToHex(true));
            return output.ReturnValue1;
        }

        public async Task<BigInteger> GetERC20AllowanceAsync(string contractAddress, string owner, string spender)
        {
            var allowanceFunction = new AllowanceFunction { Owner = owner, Spender = spender };
            var callData = allowanceFunction.GetCallData();
            var result = await Node.CallAsync(contractAddress, callData);

            if (!result.Success)
                throw new Exception($"allowance call failed: {result.RevertReason}");

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<AllowanceOutputDTO>(result.ReturnData.ToHex(true));
            return output.ReturnValue1;
        }

        public async Task<TransactionExecutionResult> TransferERC20Async(string contractAddress, string to, BigInteger amount)
        {
            var transferFunction = new TransferFunction { To = to, Value = amount };
            var callData = transferFunction.GetCallData();
            var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
            return await Node.SendTransactionAsync(signedTx);
        }

        public async Task<TransactionExecutionResult> ApproveERC20Async(string contractAddress, string spender, BigInteger amount)
        {
            var approveFunction = new ApproveFunction { Spender = spender, Value = amount };
            var callData = approveFunction.GetCallData();
            var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
            return await Node.SendTransactionAsync(signedTx);
        }

        public async Task<TransactionExecutionResult> TransferFromERC20Async(
            string contractAddress,
            string from,
            string to,
            BigInteger amount,
            string signerPrivateKey = null)
        {
            var transferFromFunction = new TransferFromFunction { From = from, To = to, Value = amount };
            var callData = transferFromFunction.GetCallData();

            var privateKey = signerPrivateKey ?? PrivateKey;
            var nonce = await Node.GetNonceAsync(Address);
            BigInteger gasPrice = 1_000_000_000;
            BigInteger gasLimit = 500_000;

            var signedTxHex = _signer.SignTransaction(
                privateKey.HexToByteArray(),
                ChainId,
                contractAddress,
                BigInteger.Zero,
                nonce,
                gasPrice,
                gasLimit,
                callData.ToHex());

            var signedTx = TransactionFactory.CreateTransaction(signedTxHex);
            return await Node.SendTransactionAsync(signedTx);
        }

        public ISignedTransaction CreateSignedTransactionFrom2(
            string to,
            BigInteger value,
            byte[]? data = null,
            BigInteger? gasLimit = null,
            BigInteger? gasPrice = null,
            BigInteger? nonce = null)
        {
            var txNonce = nonce ?? Node.GetNonceAsync(Address2).Result;
            var txGasPrice = gasPrice ?? 1_000_000_000;
            var txGasLimit = gasLimit ?? (data != null ? 500_000 : 21_000);

            var signedTxHex = _signer2.SignTransaction(
                PrivateKey2.HexToByteArray(),
                ChainId,
                to,
                value,
                txNonce,
                txGasPrice,
                txGasLimit,
                data?.ToHex() ?? "");

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        public async Task<TransactionExecutionResult> TransferFromERC20AsSpenderAsync(
            string contractAddress,
            string from,
            string to,
            BigInteger amount)
        {
            var transferFromFunction = new TransferFromFunction { From = from, To = to, Value = amount };
            var callData = transferFromFunction.GetCallData();
            var signedTx = CreateSignedTransactionFrom2(contractAddress, BigInteger.Zero, callData);
            return await Node.SendTransactionAsync(signedTx);
        }
    }
}
