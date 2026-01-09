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

                // Ensure ChainId is set for EVM execution
                if (callInput.ChainId == null)
                {
                    callInput.ChainId = new Hex.HexTypes.HexBigInteger(chainId);
                }

                var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
                var executionStateService = new ExecutionStateService(nodeDataService);

                var timestamp = block?.Timestamp?.Value != null
                    ? (long)block.Timestamp.Value
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var programContext = new ProgramContext(
                    callInput,
                    executionStateService,
                    null,
                    null,
                    (long)blockNumber.Value,
                    timestamp);

                var program = new Program(code.HexToByteArray(), programContext);
                var evmSimulator = new EVMSimulator();

                ct.ThrowIfCancellationRequested();

                program = await evmSimulator.ExecuteAsync(program, 0, 0, true).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                // Collect all unique contract addresses from logs and inner calls
                var allAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                allAddresses.Add(contractAddress);

                if (program.ProgramResult.Logs != null)
                {
                    foreach (var log in program.ProgramResult.Logs)
                    {
                        if (!string.IsNullOrEmpty(log.Address))
                        {
                            allAddresses.Add(log.Address);
                        }
                    }
                }

                if (program.ProgramResult.InnerCalls != null)
                {
                    CollectAddressesFromCalls(program.ProgramResult.InnerCalls, allAddresses);
                }

                // Pre-fetch ABIs for all contract addresses in parallel
                var fetchTasks = allAddresses.Select(addr =>
                    _abiStorage.GetABIInfoAsync(chainId, addr)).ToList();
                await Task.WhenAll(fetchTasks).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                var decoder = new ProgramResultDecoder(_abiStorage);
                var decodedResult = decoder.Decode(program, callInput, new BigInteger(chainId));

                if (decodedResult == null)
                {
                    return new StateChangesResult { Error = "Failed to decode program result" };
                }

                var extractor = new StateChangesExtractor();
                var stateChanges = extractor.ExtractFromDecodedResult(
                    decodedResult,
                    executionStateService,
                    currentUserAddress);

                if (program.ProgramResult.IsRevert)
                {
                    stateChanges.Error = "Transaction would revert";
                }

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
