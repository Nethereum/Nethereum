using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.ABIRepository;
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
                    stateChanges.Error = execResult.RevertReason ?? execResult.Error ?? "Transaction would revert";
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
                Sender = callInput.From,
                To = callInput.To,
                Data = callInput.Data?.HexToByteArray(),
                GasLimit = gasLimit,
                Value = value,
                GasPrice = gasPrice,
                MaxFeePerGas = callInput.MaxFeePerGas?.Value ?? gasPrice,
                MaxPriorityFeePerGas = callInput.MaxPriorityFeePerGas?.Value ?? 1_000_000_000,
                Nonce = callInput.Nonce?.Value ?? BigInteger.Zero,
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
