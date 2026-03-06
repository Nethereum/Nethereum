using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Decoding;
using Nethereum.EVM.Execution;
using Nethereum.EVM.StateChanges;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.TokenServices.ERC20;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.UI;
using Nethereum.Web3;

namespace Nethereum.Wallet.Services.Transaction
{
    public class StateChangesPreviewService : IStateChangesPreviewService
    {
        private static readonly Dictionary<string, string> WellKnownErrors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["0x93dafdf1"] = "SafeCastOverflow()",
            ["0x4e487b71"] = "Panic(uint256)",
            ["0x08c379a0"] = "Error(string)",
            ["0x098fb561"] = "InsufficientInputAmount()",
            ["0x42301c23"] = "InsufficientOutputAmount()",
            ["0xbb55fd27"] = "InsufficientLiquidity()",
            ["0x203d82d8"] = "Expired()",
            ["0x8baa579f"] = "InvalidSignature()",
            ["0xe450d38c"] = "ERC20InsufficientBalance(address,uint256,uint256)",
            ["0xfb8f41b2"] = "ERC20InsufficientAllowance(address,uint256,uint256)",
            ["0xf4d678b8"] = "InsufficientBalance()",
            ["0xf9067066"] = "AllowanceOverflow()",
            ["0x8301ab38"] = "AllowanceUnderflow()",
            ["0x4c14f64c"] = "InvalidSender(address)",
            ["0x9cfea583"] = "InvalidReceiver(address)",
        };

        private readonly IRpcClientFactory _rpcClientFactory;
        private readonly IChainManagementService _chainManagementService;
        private readonly IErc20TokenService _tokenService;
        private readonly IABIInfoStorage _abiStorage;

        public StateChangesPreviewService(
            IRpcClientFactory rpcClientFactory,
            IChainManagementService chainManagementService,
            IErc20TokenService tokenService,
            IABIInfoStorage abiStorage)
        {
            _rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _abiStorage = abiStorage ?? throw new ArgumentNullException(nameof(abiStorage));
        }

        public async Task<StateChangesResult> PreviewStateChangesAsync(
            CallInput callInput,
            long chainId,
            string currentUserAddress,
            CancellationToken ct = default)
        {
            return await PreviewStateChangesAsync(callInput, chainId, currentUserAddress, enableTracing: true, ct).ConfigureAwait(false);
        }

        public async Task<StateChangesResult> PreviewStateChangesAsync(
            CallInput callInput,
            long chainId,
            string currentUserAddress,
            bool enableTracing,
            CancellationToken ct = default)
        {
            try
            {
                var chain = await _chainManagementService.GetChainAsync(new BigInteger(chainId)).ConfigureAwait(false);
                if (chain == null)
                {
                    return new StateChangesResult { Error = $"Chain {chainId} not found" };
                }

                var client = await _rpcClientFactory.CreateClientAsync(chain).ConfigureAwait(false);
                var web3 = new Web3.Web3(client);

                var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
                var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(blockNumber).ConfigureAwait(false);

                var contractAddress = callInput.To;
                if (string.IsNullOrEmpty(contractAddress))
                {
                    return new StateChangesResult { Error = "Contract address is required for state changes preview" };
                }

                var code = await web3.Eth.GetCode.SendRequestAsync(contractAddress).ConfigureAwait(false);
                if (string.IsNullOrEmpty(code) || code == "0x")
                {
                    return ExtractSimpleTransferChanges(callInput, currentUserAddress);
                }

                if (callInput.ChainId == null)
                {
                    callInput.ChainId = new Hex.HexTypes.HexBigInteger(chainId);
                }

                try
                {
                    await web3.Eth.Transactions.Call.SendRequestAsync(callInput, new BlockParameter(blockNumber)).ConfigureAwait(false);
                }
                catch (Exception ethCallEx)
                {
                    return new StateChangesResult { Error = $"eth_call failed: {ethCallEx.Message}" };
                }

                var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
                var executionStateService = new ExecutionStateService(nodeDataService);

                var timestamp = block?.Timestamp?.Value != null
                    ? (long)block.Timestamp.Value
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var baseFee = block?.BaseFeePerGas?.Value ?? BigInteger.Zero;

                ct.ThrowIfCancellationRequested();

                var ctx = BuildExecutionContext(callInput, executionStateService, blockNumber, timestamp, baseFee, enableTracing);

                var config = HardforkConfig.Default;
                var executor = new TransactionExecutor(config);
                var execResult = await executor.ExecuteAsync(ctx).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                var allAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                allAddresses.Add(contractAddress);

                if (execResult.Logs != null)
                {
                    foreach (var log in execResult.Logs)
                    {
                        if (!string.IsNullOrEmpty(log.Address))
                        {
                            allAddresses.Add(log.Address);
                        }
                    }
                }

                if (execResult.InnerCalls != null)
                {
                    CollectAddressesFromCalls(execResult.InnerCalls, allAddresses);
                }

                if (execResult.ProgramResult?.InnerCallResults != null)
                {
                    foreach (var icr in execResult.ProgramResult.InnerCallResults)
                    {
                        if (!string.IsNullOrEmpty(icr.CallInput?.To))
                            allAddresses.Add(icr.CallInput.To);
                    }
                }

                var fetchTasks = allAddresses.Select(addr =>
                    _abiStorage.GetABIInfoAsync(chainId, addr)).ToList();
                await Task.WhenAll(fetchTasks).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                var decoder = new ProgramResultDecoder(_abiStorage);
                var decodedResult = decoder.Decode(execResult, callInput, new BigInteger(chainId));

                if (decodedResult == null)
                {
                    return new StateChangesResult { Error = "Failed to decode program result" };
                }

                var extractor = new StateChangesExtractor();
                var stateChanges = extractor.ExtractFromDecodedResult(
                    decodedResult,
                    executionStateService,
                    currentUserAddress);

                if (!execResult.Success)
                {
                    var revertMsg = execResult.RevertReason ?? execResult.Error;
                    if (string.IsNullOrEmpty(revertMsg) && execResult.ReturnData != null && execResult.ReturnData.Length >= 4)
                    {
                        revertMsg = await DecodeCustomErrorAsync(execResult.ReturnData, contractAddress, chainId).ConfigureAwait(false);
                    }
                    stateChanges.Error = revertMsg ?? "Transaction would revert";
                }

                stateChanges.Traces = execResult.Traces;
                stateChanges.GasUsed = execResult.GasUsed;

                await AddTokenMetadataAsync(stateChanges, chainId).ConfigureAwait(false);

                return stateChanges;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new StateChangesResult { Error = $"Preview failed: {ex.Message}" };
            }
        }

        private TransactionExecutionContext BuildExecutionContext(
            CallInput callInput,
            ExecutionStateService executionState,
            Hex.HexTypes.HexBigInteger blockNumber,
            long timestamp,
            BigInteger baseFee,
            bool traceEnabled)
        {
            var gasLimit = callInput.Gas?.Value ?? 10_000_000;
            var value = callInput.Value?.Value ?? BigInteger.Zero;
            var gasPrice = callInput.GasPrice?.Value ?? baseFee + 1_000_000_000;

            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = callInput.From,
                To = callInput.To,
                Data = callInput.Data?.HexToByteArray(),
                GasLimit = gasLimit,
                Value = value,
                GasPrice = gasPrice,
                MaxFeePerGas = callInput.MaxFeePerGas?.Value ?? gasPrice,
                MaxPriorityFeePerGas = callInput.MaxPriorityFeePerGas?.Value ?? 1_000_000_000,
                Nonce = BigInteger.Zero,
                IsEip1559 = callInput.MaxFeePerGas != null,
                IsContractCreation = string.IsNullOrEmpty(callInput.To),
                BlockNumber = (long)blockNumber.Value,
                Timestamp = timestamp,
                BaseFee = baseFee,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BlockGasLimit = 30_000_000,
                ExecutionState = executionState,
                TraceEnabled = traceEnabled
            };
        }

        private StateChangesResult ExtractSimpleTransferChanges(CallInput callInput, string currentUserAddress)
        {
            var result = new StateChangesResult();

            if (callInput.Value != null && callInput.Value.Value > 0)
            {
                var from = callInput.From?.ToLowerInvariant() ?? "";
                var to = callInput.To?.ToLowerInvariant() ?? "";
                var amount = callInput.Value.Value;

                if (!string.IsNullOrEmpty(from))
                {
                    result.BalanceChanges.Add(new BalanceChange
                    {
                        Address = from,
                        Type = BalanceChangeType.Native,
                        Change = -amount,
                        IsCurrentUser = from.Equals(currentUserAddress?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
                    });
                }

                if (!string.IsNullOrEmpty(to))
                {
                    result.BalanceChanges.Add(new BalanceChange
                    {
                        Address = to,
                        Type = BalanceChangeType.Native,
                        Change = amount,
                        IsCurrentUser = to.Equals(currentUserAddress?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            return result;
        }

        private async Task AddTokenMetadataAsync(StateChangesResult result, long chainId)
        {
            if (result?.BalanceChanges == null) return;

            foreach (var change in result.BalanceChanges)
            {
                if (change.Type != BalanceChangeType.Native &&
                    !string.IsNullOrEmpty(change.TokenAddress) &&
                    string.IsNullOrEmpty(change.TokenSymbol))
                {
                    try
                    {
                        var tokenInfo = await _tokenService.GetTokenAsync(chainId, change.TokenAddress).ConfigureAwait(false);
                        if (tokenInfo != null)
                        {
                            change.TokenSymbol = tokenInfo.Symbol;
                            change.TokenDecimals = tokenInfo.Decimals;
                        }
                    }
                    catch
                    {
                        // Token not found in cache, leave symbol empty
                    }
                }
            }
        }

        private async Task<string> DecodeCustomErrorAsync(byte[] revertData, string contractAddress, long chainId)
        {
            try
            {
                var revertHex = revertData.ToHex(true);
                var selector = revertHex.Substring(0, 10);

                await _abiStorage.GetABIInfoAsync(chainId, contractAddress).ConfigureAwait(false);

                var errorAbi = await _abiStorage.FindErrorABIAsync(
                    new BigInteger(chainId), contractAddress, selector).ConfigureAwait(false);

                if (errorAbi != null)
                {
                    var decoder = new FunctionCallDecoder();
                    var decoded = decoder.DecodeError(errorAbi, revertHex);
                    var paramStr = decoded != null && decoded.Count > 0
                        ? string.Join(", ", decoded.Select(p => $"{p.Parameter.Name}: {p.Result}"))
                        : "";
                    return string.IsNullOrEmpty(paramStr)
                        ? errorAbi.Name
                        : $"{errorAbi.Name}({paramStr})";
                }

                var errorAbis = _abiStorage.FindErrorABI(selector);
                if (errorAbis != null && errorAbis.Count > 0)
                {
                    var firstMatch = errorAbis[0];
                    var decoder = new FunctionCallDecoder();
                    var decoded = decoder.DecodeError(firstMatch, revertHex);
                    var paramStr = decoded != null && decoded.Count > 0
                        ? string.Join(", ", decoded.Select(p => $"{p.Parameter.Name}: {p.Result}"))
                        : "";
                    return string.IsNullOrEmpty(paramStr)
                        ? firstMatch.Name
                        : $"{firstMatch.Name}({paramStr})";
                }

                if (WellKnownErrors.TryGetValue(selector, out var knownError))
                    return knownError;

                return $"Custom error: {selector}";
            }
            catch
            {
                return $"Reverted with data: {revertData.ToHex(true)}";
            }
        }

        private void CollectAddressesFromCalls(List<CallInput> calls, HashSet<string> addresses)
        {
            if (calls == null) return;
            foreach (var call in calls)
            {
                if (!string.IsNullOrEmpty(call.To))
                {
                    addresses.Add(call.To);
                }
            }
        }
    }
}
