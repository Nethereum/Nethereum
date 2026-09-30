using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, string from = null,
            BigInteger? value = null, BigInteger? gasLimit = null)
        {
            var blockContext = await GetBlockContextForCallAsync();
            return await CreateAccessListCoreAsync(_nodeDataService, blockContext, to, data, from, value, gasLimit);
        }

        public virtual async Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            return await CreateAccessListCoreAsync(dataService, blockContext, to, data, from, value, gasLimit);
        }

        private async Task<AccessListResult> CreateAccessListCoreAsync(
            IStateReader dataService, BlockContext blockContext, string to, byte[] data,
            string from, BigInteger? value, BigInteger? gasLimit)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = ResolveCallGas(gasLimit);
            var isContractCreation = string.IsNullOrEmpty(to);

            var callerBalance = await dataService.GetBalanceAsync(from);

            var discovered = await ExecuteAccessListPassAsync(
                dataService, blockContext, from, to, isContractCreation, callValue, data, callGasLimit, callerBalance, warmSeed: null);

            var accessList = ExcludePrecompiles(discovered.AccessList,
                ResolveHardforkConfig((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).Precompiles);

            var warm = await ExecuteAccessListPassAsync(
                dataService, blockContext, from, to, isContractCreation, callValue, data, callGasLimit, callerBalance,
                warmSeed: accessList);

            return new AccessListResult
            {
                AccessList = accessList,
                GasUsed = warm.GasUsed,
                Error = warm.Error
            };
        }

        private static List<AccessListItem> ExcludePrecompiles(List<AccessListItem> accessList, PrecompileRegistry precompiles)
        {
            if (accessList.Count == 0 || precompiles == null) return accessList;

            var precompileAddresses = new HashSet<string>();
            foreach (var index in precompiles.GetAddresses())
                precompileAddresses.Add("0x" + index.ToString("x40"));

            var result = new List<AccessListItem>(accessList.Count);
            foreach (var item in accessList)
                if (!precompileAddresses.Contains(item.Address))
                    result.Add(item);
            return result;
        }

        private async Task<(List<AccessListItem> AccessList, long GasUsed, string Error)> ExecuteAccessListPassAsync(
            IStateReader dataService, BlockContext blockContext, string from, string to, bool isContractCreation,
            BigInteger callValue, byte[] data, BigInteger callGasLimit, EvmUInt256 callerBalance,
            List<AccessListItem> warmSeed)
        {
            var executionStateService = new ExecutionStateService(dataService);
            executionStateService.SetInitialChainBalance(from, callerBalance);
            if (warmSeed != null) PreWarmAccessList(executionStateService, warmSeed);

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = isContractCreation ? null : to,
                Data = data,
                Value = callValue,
                GasLimit = callGasLimit,
                GasPrice = 0,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = false,
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                SlotNumber = blockContext.SlotNumber.HasValue
                    ? new EvmUInt256(blockContext.SlotNumber.Value)
                    : EvmUInt256.Zero,
                ExecutionState = executionStateService,
                TraceEnabled = false,
                TrackAccessList = true,
                EnforceSenderBalance = false,
                AdvanceSenderNonce = false
            };

            var result = await ResolveExecutor((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).ExecuteAsync(ctx);

            var accessList = result.Program?.ProgramContext.GetAccessList() ?? new List<AccessListItem>();
            var error = result.Success ? null : (result.RevertReason ?? result.Error ?? "execution reverted");
            return (accessList, result.GasUsed, error);
        }

        private static void PreWarmAccessList(ExecutionStateService executionStateService, List<AccessListItem> accessList)
        {
            foreach (var entry in accessList)
            {
                executionStateService.MarkAddressAsWarm(entry.Address);
                if (entry.StorageKeys == null) continue;
                var account = executionStateService.CreateOrGetAccountExecutionState(entry.Address);
                foreach (var key in entry.StorageKeys)
                    account.MarkStorageKeyAsWarm(EvmUInt256.FromBigEndian(key));
            }
        }
    }
}
