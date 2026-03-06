using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Anchoring
{
    public class EvmAnchorService : IAnchorService
    {
        private readonly AnchorConfig _config;
        private readonly Web3.Web3? _web3;
        private readonly ILogger<EvmAnchorService>? _logger;

        public EvmAnchorService(AnchorConfig config, ILogger<EvmAnchorService>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;

            if (!string.IsNullOrEmpty(_config.TargetRpcUrl) && !string.IsNullOrEmpty(_config.SequencerPrivateKey))
            {
                var account = new Account(_config.SequencerPrivateKey, _config.TargetChainId);
                _web3 = new Web3.Web3(account, _config.TargetRpcUrl);
            }
        }

        public async Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            var anchorInfo = new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (_web3 == null || string.IsNullOrEmpty(_config.AnchorContractAddress))
            {
                _logger?.LogWarning("Anchor service not configured, skipping anchor for block {BlockNumber}", blockNumber);
                anchorInfo.Status = AnchorStatus.Failed;
                anchorInfo.ErrorMessage = "Anchor service not configured";
                return anchorInfo;
            }

            try
            {
                _logger?.LogInformation("Anchoring block {BlockNumber} to {Contract}",
                    blockNumber, _config.AnchorContractAddress);

                var anchorFunction = new EvmAnchorFunction
                {
                    BlockNumber = blockNumber,
                    StateRoot = stateRoot,
                    TxRoot = transactionsRoot,
                    ReceiptRoot = receiptsRoot
                };

                var handler = _web3.Eth.GetContractTransactionHandler<EvmAnchorFunction>();
                var receipt = await handler.SendRequestAndWaitForReceiptAsync(
                    _config.AnchorContractAddress, anchorFunction);

                if (receipt.Succeeded())
                {
                    anchorInfo.AnchorTxHash = receipt.TransactionHash.HexToByteArray();
                    anchorInfo.AnchorBlockNumber = receipt.BlockNumber.Value;
                    anchorInfo.Status = AnchorStatus.Confirmed;

                    _logger?.LogInformation("Block {BlockNumber} anchored in tx {TxHash}",
                        blockNumber, receipt.TransactionHash);
                }
                else
                {
                    anchorInfo.Status = AnchorStatus.Failed;
                    anchorInfo.ErrorMessage = "Transaction failed";

                    _logger?.LogError("Failed to anchor block {BlockNumber}: transaction failed", blockNumber);
                }
            }
            catch (Exception ex)
            {
                anchorInfo.Status = AnchorStatus.Failed;
                anchorInfo.ErrorMessage = ex.Message;

                _logger?.LogError(ex, "Failed to anchor block {BlockNumber}", blockNumber);
            }

            return anchorInfo;
        }

        public async Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            if (_web3 == null || string.IsNullOrEmpty(_config.AnchorContractAddress))
            {
                return null;
            }

            try
            {
                var getAnchorFunction = new EvmGetAnchorFunction { BlockNumber = blockNumber };
                var handler = _web3.Eth.GetContractQueryHandler<EvmGetAnchorFunction>();
                var result = await handler.QueryDeserializingToObjectAsync<EvmAnchorOutputDTO>(
                    getAnchorFunction, _config.AnchorContractAddress);

                if (result.Timestamp == 0)
                {
                    return null;
                }

                return new AnchorInfo
                {
                    BlockNumber = blockNumber,
                    StateRoot = result.StateRoot,
                    TransactionsRoot = result.TxRoot,
                    ReceiptsRoot = result.ReceiptRoot,
                    Timestamp = (long)result.Timestamp,
                    Status = AnchorStatus.Confirmed
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get anchor for block {BlockNumber}", blockNumber);
                return null;
            }
        }

        public async Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            if (_web3 == null || string.IsNullOrEmpty(_config.AnchorContractAddress))
            {
                return BigInteger.Zero;
            }

            try
            {
                var latestBlockFunction = new EvmLatestBlockFunction();
                var handler = _web3.Eth.GetContractQueryHandler<EvmLatestBlockFunction>();
                return await handler.QueryAsync<BigInteger>(
                    _config.AnchorContractAddress, latestBlockFunction);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get latest anchored block");
                return BigInteger.Zero;
            }
        }

        public async Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            var anchor = await GetAnchorAsync(blockNumber);
            if (anchor == null)
            {
                return false;
            }

            return ByteArraysEqual(anchor.StateRoot, stateRoot) &&
                   ByteArraysEqual(anchor.TransactionsRoot, transactionsRoot) &&
                   ByteArraysEqual(anchor.ReceiptsRoot, receiptsRoot);
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }

    [Function("anchor")]
    public class EvmAnchorFunction : FunctionMessage
    {
        [Parameter("uint256", "blockNumber", 1)]
        public BigInteger BlockNumber { get; set; }

        [Parameter("bytes32", "stateRoot", 2)]
        public byte[] StateRoot { get; set; } = new byte[32];

        [Parameter("bytes32", "txRoot", 3)]
        public byte[] TxRoot { get; set; } = new byte[32];

        [Parameter("bytes32", "receiptRoot", 4)]
        public byte[] ReceiptRoot { get; set; } = new byte[32];
    }

    [Function("getAnchor", typeof(EvmAnchorOutputDTO))]
    public class EvmGetAnchorFunction : FunctionMessage
    {
        [Parameter("uint256", "blockNumber", 1)]
        public BigInteger BlockNumber { get; set; }
    }

    [FunctionOutput]
    public class EvmAnchorOutputDTO : IFunctionOutputDTO
    {
        [Parameter("bytes32", "stateRoot", 1)]
        public byte[] StateRoot { get; set; } = new byte[32];

        [Parameter("bytes32", "txRoot", 2)]
        public byte[] TxRoot { get; set; } = new byte[32];

        [Parameter("bytes32", "receiptRoot", 3)]
        public byte[] ReceiptRoot { get; set; } = new byte[32];

        [Parameter("uint256", "timestamp", 4)]
        public BigInteger Timestamp { get; set; }
    }

    [Function("latestBlock", "uint256")]
    public class EvmLatestBlockFunction : FunctionMessage
    {
    }
}
