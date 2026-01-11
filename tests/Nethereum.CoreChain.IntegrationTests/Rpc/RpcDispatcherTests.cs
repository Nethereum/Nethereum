using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Contracts;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class RpcDispatcherTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly RpcDispatcher _dispatcher;
        private readonly RpcContext _context;

        public RpcDispatcherTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            _context = new RpcContext(_fixture.Node, _fixture.ChainId, services);
            _dispatcher = new RpcDispatcher(registry, _context);
        }

        [Fact]
        public async Task EthChainId_ReturnsConfiguredChainId()
        {
            var request = new RpcRequestMessage(1, "eth_chainId");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            // ChainId 31337 = 0x7a69 - HexBigInteger.HexValue returns hex string
            var result = response.Result;
            if (result is Nethereum.Hex.HexTypes.HexBigInteger hexBigInt)
            {
                Assert.Equal("0x7a69", hexBigInt.HexValue);
            }
            else
            {
                Assert.Equal(31337.ToString(), result?.ToString());
            }
        }

        [Fact]
        public async Task EthBlockNumber_ReturnsCurrentBlockNumber()
        {
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();

            var request = new RpcRequestMessage(1, "eth_blockNumber");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetBalance_ReturnsAccountBalance()
        {
            var request = new RpcRequestMessage(1, "eth_getBalance",
                _fixture.Address, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetTransactionCount_ReturnsNonce()
        {
            var request = new RpcRequestMessage(1, "eth_getTransactionCount",
                _fixture.Address, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetCode_ReturnsEmptyForEOA()
        {
            var request = new RpcRequestMessage(1, "eth_getCode",
                _fixture.Address, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            // EOA has no code, should return "0x" or empty
            var result = response.Result?.ToString();
            Assert.True(result == "0x" || result == null || result == "",
                $"Expected empty code for EOA, got: {result}");
        }

        [Fact]
        public async Task EthGetStorageAt_ReturnsStorageValue()
        {
            var request = new RpcRequestMessage(1, "eth_getStorageAt",
                _fixture.Address, "0x0", "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task EthGasPrice_ReturnsBaseFee()
        {
            var request = new RpcRequestMessage(1, "eth_gasPrice");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetBlockByNumber_ReturnsBlock()
        {
            var request = new RpcRequestMessage(1, "eth_getBlockByNumber",
                "0x0", false);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetBlockByNumber_WithTransactions_ReturnsFullBlock()
        {
            // First send a transaction to have something in a block
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var blockNumberHex = new HexBigInteger(blockNumber).HexValue;

            var request = new RpcRequestMessage(1, "eth_getBlockByNumber",
                blockNumberHex, true);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task NetVersion_ReturnsChainId()
        {
            var request = new RpcRequestMessage(1, "net_version");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Equal(_fixture.ChainId.ToString(), response.Result.ToString());
        }

        [Fact]
        public async Task Web3ClientVersion_ReturnsVersion()
        {
            var request = new RpcRequestMessage(1, "web3_clientVersion");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Contains("Nethereum", response.Result.ToString());
        }

        [Fact]
        public async Task InvalidMethod_ReturnsMethodNotFound()
        {
            var request = new RpcRequestMessage(1, "eth_invalidMethod");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal(-32601, response.Error.Code);
        }

        [Fact]
        public async Task NullRequest_ReturnsInvalidRequest()
        {
            var response = await _dispatcher.DispatchAsync(null!);

            Assert.NotNull(response.Error);
            Assert.Equal(-32600, response.Error.Code);
        }

        [Fact]
        public async Task EthGetTransactionReceipt_ReturnsReceipt()
        {
            // First send a transaction
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var txHashHex = "0x" + BitConverter.ToString(signedTx.Hash).Replace("-", "").ToLowerInvariant();

            var request = new RpcRequestMessage(1, "eth_getTransactionReceipt", txHashHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetBlockByHash_ReturnsBlock()
        {
            var blockHash = await _fixture.Node.MineBlockAsync();
            var blockHashHex = "0x" + BitConverter.ToString(blockHash).Replace("-", "").ToLowerInvariant();

            var request = new RpcRequestMessage(1, "eth_getBlockByHash",
                blockHashHex, false);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetLogs_ReturnsEmptyForNoLogs()
        {
            await _fixture.Node.MineBlockAsync();

            var filter = JObject.FromObject(new { fromBlock = "0x0", toBlock = "latest" });
            var request = new RpcRequestMessage(1, "eth_getLogs", filter);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetLogs_ReturnsLogsFromContractEvent()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var transferAmount = BigInteger.Parse("100000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            var filter = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest",
                address = contractAddress
            });
            var request = new RpcRequestMessage(1, "eth_getLogs", filter);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var logs = response.Result as IEnumerable<object>;
            Assert.NotNull(logs);
            Assert.NotEmpty(logs);
        }

        [Fact]
        public async Task EthGetLogs_FiltersByBlockRange()
        {
            await _fixture.Node.MineBlockAsync();
            var startBlock = await _fixture.Node.GetBlockNumberAsync();

            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);
            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, BigInteger.Parse("100000000000000000000"));

            var endBlock = await _fixture.Node.GetBlockNumberAsync();

            var filterBefore = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = new HexBigInteger(startBlock - 1).HexValue,
                address = contractAddress
            });
            var requestBefore = new RpcRequestMessage(1, "eth_getLogs", filterBefore);
            var responseBefore = await _dispatcher.DispatchAsync(requestBefore);

            Assert.Null(responseBefore.Error);
            var logsBefore = responseBefore.Result as IEnumerable<object>;
            Assert.NotNull(logsBefore);
            Assert.Empty(logsBefore);

            var filterAfter = JObject.FromObject(new
            {
                fromBlock = new HexBigInteger(startBlock).HexValue,
                toBlock = new HexBigInteger(endBlock).HexValue,
                address = contractAddress
            });
            var requestAfter = new RpcRequestMessage(1, "eth_getLogs", filterAfter);
            var responseAfter = await _dispatcher.DispatchAsync(requestAfter);

            Assert.Null(responseAfter.Error);
            var logsAfter = responseAfter.Result as IEnumerable<object>;
            Assert.NotNull(logsAfter);
            Assert.NotEmpty(logsAfter);
        }

        [Fact]
        public async Task EthGetLogs_FiltersByAddress()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contract1 = await _fixture.DeployERC20Async(initialBalance);
            var contract2 = await _fixture.DeployERC20Async(initialBalance);

            await _fixture.TransferERC20Async(contract1, _fixture.RecipientAddress, BigInteger.Parse("10000000000000000000"));
            await _fixture.TransferERC20Async(contract2, _fixture.RecipientAddress, BigInteger.Parse("10000000000000000000"));

            var filter1 = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest",
                address = contract1
            });
            var request1 = new RpcRequestMessage(1, "eth_getLogs", filter1);
            var response1 = await _dispatcher.DispatchAsync(request1);

            Assert.Null(response1.Error);
            var logs1 = response1.Result as IEnumerable<object>;
            Assert.NotNull(logs1);
            foreach (var log in logs1)
            {
                var logJson = JObject.FromObject(log);
                var address = logJson["address"]?.ToString();
                Assert.Equal(contract1.ToLowerInvariant(), address?.ToLowerInvariant());
            }
        }

        [Fact]
        public async Task EthGetLogs_ReturnsLogsWithCorrectStructure()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, BigInteger.Parse("100000000000000000000"));

            var filter = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest",
                address = contractAddress
            });
            var request = new RpcRequestMessage(1, "eth_getLogs", filter);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var logs = response.Result as IEnumerable<object>;
            Assert.NotNull(logs);

            var firstLogJson = JObject.FromObject(logs.First());
            Assert.NotNull(firstLogJson["address"]);
            Assert.NotNull(firstLogJson["blockHash"]);
            Assert.NotNull(firstLogJson["blockNumber"]);
            Assert.NotNull(firstLogJson["transactionHash"]);
            Assert.NotNull(firstLogJson["logIndex"]);
            Assert.NotNull(firstLogJson["topics"]);
        }

        [Fact]
        public async Task EthGetProof_ReturnsAccountProof()
        {
            var request = new RpcRequestMessage(1, "eth_getProof",
                _fixture.Address, new JArray(), "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = JObject.FromObject(response.Result);
            Assert.NotNull(result["address"]);
            Assert.NotNull(result["balance"]);
            Assert.NotNull(result["nonce"]);
            Assert.NotNull(result["codeHash"]);
            Assert.NotNull(result["storageHash"]);
            Assert.NotNull(result["accountProof"]);
        }

        [Fact]
        public async Task EthGetProof_ReturnsStorageProof()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var storageKeys = new JArray { "0x0" };
            var request = new RpcRequestMessage(1, "eth_getProof",
                contractAddress, storageKeys, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = JObject.FromObject(response.Result);
            Assert.NotNull(result["storageProof"]);
            var storageProofs = result["storageProof"] as JArray;
            Assert.NotNull(storageProofs);
            Assert.Single(storageProofs);

            var storageProof = storageProofs[0] as JObject;
            Assert.NotNull(storageProof);
            Assert.NotNull(storageProof["key"]);
            Assert.NotNull(storageProof["value"]);
            Assert.NotNull(storageProof["proof"]);
        }

        [Fact]
        public async Task EthGetProof_ReturnsCorrectBalance()
        {
            var request = new RpcRequestMessage(1, "eth_getProof",
                _fixture.Address, new JArray(), "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = JObject.FromObject(response.Result);
            var balanceHex = result["balance"]?.ToString();
            Assert.NotNull(balanceHex);
            Assert.StartsWith("0x", balanceHex);
        }

        [Fact]
        public async Task EthGetProof_ReturnsEmptyCodeHashForEOA()
        {
            var request = new RpcRequestMessage(1, "eth_getProof",
                _fixture.Address, new JArray(), "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = JObject.FromObject(response.Result);
            var codeHash = result["codeHash"]?.ToString();
            Assert.NotNull(codeHash);
            Assert.Equal("0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", codeHash);
        }

        [Fact]
        public async Task EthGetProof_ReturnsMultipleStorageProofs()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var storageKeys = new JArray { "0x0", "0x1", "0x2" };
            var request = new RpcRequestMessage(1, "eth_getProof",
                contractAddress, storageKeys, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = JObject.FromObject(response.Result);
            var storageProofs = result["storageProof"] as JArray;
            Assert.NotNull(storageProofs);
            Assert.Equal(3, storageProofs.Count);
        }

        [Fact]
        public async Task EthGetProof_ReturnsNonEmptyAccountProof()
        {
            var request = new RpcRequestMessage(1, "eth_getProof",
                _fixture.Address, new JArray(), "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = JObject.FromObject(response.Result);
            var accountProof = result["accountProof"] as JArray;
            Assert.NotNull(accountProof);
            Assert.NotEmpty(accountProof);
        }

        #region eth_sendRawTransaction Tests

        [Fact]
        public async Task EthSendRawTransaction_ValidTransaction_ReturnsTxHash()
        {
            var nonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var signer = new LegacyTransactionSigner();
            var signedTxHex = signer.SignTransaction(
                _fixture.PrivateKey.HexToByteArray(),
                _fixture.ChainId,
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"),
                nonce,
                1_000_000_000,
                21_000,
                "");

            var request = new RpcRequestMessage(1, "eth_sendRawTransaction", "0x" + signedTxHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var txHash = response.Result.ToString();
            Assert.StartsWith("0x", txHash);
            Assert.Equal(66, txHash.Length);
        }

        [Fact]
        public async Task EthSendRawTransaction_InvalidRLP_ReturnsError()
        {
            var request = new RpcRequestMessage(1, "eth_sendRawTransaction", "0x1234invalid");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal(-32000, response.Error.Code);
        }

        [Fact]
        public async Task EthSendRawTransaction_EIP1559Transaction_ReturnsTxHash()
        {
            var nonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var signer = new Transaction1559Signer();
            var tx1559 = new Transaction1559(
                _fixture.ChainId,
                nonce,
                1_000_000_000,
                2_000_000_000,
                21_000,
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"),
                null,
                null);

            var signedTxHex = signer.SignTransaction(_fixture.PrivateKey.HexToByteArray(), tx1559);

            var request = new RpcRequestMessage(1, "eth_sendRawTransaction", "0x" + signedTxHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var txHash = response.Result.ToString();
            Assert.StartsWith("0x", txHash);
        }

        #endregion

        #region eth_call Tests

        [Fact]
        public async Task EthCall_ContractCall_ReturnsData()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var balanceOfFunction = new BalanceOfFunction { Account = _fixture.Address };
            var callData = balanceOfFunction.GetCallData();

            var callInput = JObject.FromObject(new
            {
                to = contractAddress,
                data = callData.ToHex(true)
            });

            var request = new RpcRequestMessage(1, "eth_call", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var result = response.Result.ToString();
            Assert.StartsWith("0x", result);
            Assert.True(result.Length > 2);
        }

        [Fact]
        public async Task EthCall_ToNonContract_ReturnsEmpty()
        {
            var callInput = JObject.FromObject(new
            {
                to = _fixture.RecipientAddress,
                data = "0x"
            });

            var request = new RpcRequestMessage(1, "eth_call", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var result = response.Result?.ToString();
            Assert.True(result == "0x" || result == null || result == "");
        }

        [Fact]
        public async Task EthCall_WithValue_Works()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var balanceOfFunction = new BalanceOfFunction { Account = _fixture.Address };
            var callData = balanceOfFunction.GetCallData();

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = callData.ToHex(true),
                value = "0x0"
            });

            var request = new RpcRequestMessage(1, "eth_call", callInput, "latest");

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        #endregion

        #region eth_estimateGas Tests

        [Fact]
        public async Task EthEstimateGas_SimpleTransfer_ReturnsEstimate()
        {
            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = _fixture.RecipientAddress,
                value = "0x" + BigInteger.Parse("100000000000000000").ToString("x")
            });

            var request = new RpcRequestMessage(1, "eth_estimateGas", callInput);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result;
            BigInteger gasEstimate;
            if (result is HexBigInteger hexBigInt)
            {
                gasEstimate = hexBigInt.Value;
            }
            else
            {
                gasEstimate = new HexBigInteger(result.ToString()).Value;
            }

            Assert.True(gasEstimate >= 21000);
        }

        [Fact]
        public async Task EthEstimateGas_ContractCall_ReturnsEstimate()
        {
            var initialBalance = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var transferFunction = new TransferFunction
            {
                To = _fixture.RecipientAddress,
                Value = BigInteger.Parse("100000000000000000000")
            };
            var callData = transferFunction.GetCallData();

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = callData.ToHex(true)
            });

            var request = new RpcRequestMessage(1, "eth_estimateGas", callInput);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result;
            BigInteger gasEstimate;
            if (result is HexBigInteger hexBigInt)
            {
                gasEstimate = hexBigInt.Value;
            }
            else
            {
                gasEstimate = new HexBigInteger(result.ToString()).Value;
            }

            Assert.True(gasEstimate > 21000);
        }

        [Fact]
        public async Task EthEstimateGas_ContractDeployment_ReturnsHigherEstimate()
        {
            var bytecode = ERC20Contract.GetDeploymentBytecode();

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                data = bytecode.ToHex(true)
            });

            var request = new RpcRequestMessage(1, "eth_estimateGas", callInput);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result;
            BigInteger gasEstimate;
            if (result is HexBigInteger hexBigInt)
            {
                gasEstimate = hexBigInt.Value;
            }
            else
            {
                gasEstimate = new HexBigInteger(result.ToString()).Value;
            }

            Assert.True(gasEstimate > 50000);
        }

        #endregion

        #region eth_getTransactionByHash Tests

        [Fact]
        public async Task EthGetTransactionByHash_ExistingTx_ReturnsTransaction()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var txHashHex = signedTx.Hash.ToHex(true);

            var request = new RpcRequestMessage(1, "eth_getTransactionByHash", txHashHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var tx = JObject.FromObject(response.Result);
            Assert.NotNull(tx["hash"]);
            Assert.NotNull(tx["from"]);
            Assert.NotNull(tx["to"]);
            Assert.NotNull(tx["value"]);
            Assert.NotNull(tx["gas"]);
            Assert.NotNull(tx["nonce"]);
            Assert.NotNull(tx["blockHash"]);
            Assert.NotNull(tx["blockNumber"]);
            Assert.NotNull(tx["transactionIndex"]);
        }

        [Fact]
        public async Task EthGetTransactionByHash_NonExistentTx_ReturnsNull()
        {
            var fakeHash = "0x" + new string('0', 64);

            var request = new RpcRequestMessage(1, "eth_getTransactionByHash", fakeHash);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.Null(response.Result);
        }

        [Fact]
        public async Task EthGetTransactionByHash_LegacyTx_HasCorrectType()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var txHashHex = signedTx.Hash.ToHex(true);

            var request = new RpcRequestMessage(1, "eth_getTransactionByHash", txHashHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var tx = JObject.FromObject(response.Result);

            var typeValue = tx["type"];
            if (typeValue != null)
            {
                var typeHex = typeValue.ToString();
                Assert.True(typeHex == "0x0" || typeHex == "0x00");
            }
        }

        [Fact]
        public async Task EthGetTransactionByHash_HasSignatureFields()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var txHashHex = signedTx.Hash.ToHex(true);

            var request = new RpcRequestMessage(1, "eth_getTransactionByHash", txHashHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var tx = JObject.FromObject(response.Result);

            Assert.NotNull(tx["r"]);
            Assert.NotNull(tx["s"]);
            Assert.NotNull(tx["v"]);
        }

        [Fact]
        public async Task EthGetTransactionByHash_ContractDeployment_HasNullTo()
        {
            var bytecode = ERC20Contract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);
            await _fixture.Node.SendTransactionAsync(signedTx);

            var txHashHex = signedTx.Hash.ToHex(true);

            var request = new RpcRequestMessage(1, "eth_getTransactionByHash", txHashHex);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var tx = JObject.FromObject(response.Result);

            var toValue = tx["to"]?.ToString();
            Assert.True(string.IsNullOrEmpty(toValue) || toValue == "null");
        }

        #endregion
    }
}
