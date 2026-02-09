using System.Numerics;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Services
{
    public class SmartAccountService : ISmartAccount
    {
        private readonly NethereumAccountService _contractService;
        private readonly IWeb3 _web3;
        private string? _entryPointAddress;

        public string Address => _contractService.ContractAddress;
        public string EntryPointAddress => _entryPointAddress ?? throw new InvalidOperationException("EntryPoint not loaded. Call LoadAsync first.");

        public SmartAccountService(IWeb3 web3, string accountAddress)
        {
            _web3 = web3;
            _contractService = new NethereumAccountService(web3, accountAddress);
        }

        public static async Task<SmartAccountService> LoadAsync(IWeb3 web3, string accountAddress)
        {
            var service = new SmartAccountService(web3, accountAddress);
            await service.LoadEntryPointAsync();
            return service;
        }

        public static async Task<SmartAccountService> CreateAsync(
            IWeb3 web3,
            ISmartAccountFactory factory,
            byte[] salt,
            byte[] initData)
        {
            var address = await factory.GetAccountAddressAsync(salt, initData);
            var isDeployed = await factory.IsDeployedAsync(salt, initData);

            if (!isDeployed)
            {
                await factory.CreateAccountAsync(salt, initData);
            }

            return await LoadAsync(web3, address);
        }

        private async Task LoadEntryPointAsync()
        {
            _entryPointAddress = await _contractService.EntryPointQueryAsync();
        }

        public async Task<BigInteger> GetNonceAsync(BigInteger key = default)
        {
            return await _contractService.GetNonceQueryAsync(key);
        }

        public async Task<BigInteger> GetDepositAsync()
        {
            return await _contractService.GetDepositQueryAsync();
        }

        public async Task<bool> IsDeployedAsync()
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(Address);
            return !string.IsNullOrEmpty(code) && code != "0x";
        }

        public async Task<TransactionReceipt> ExecuteAsync(byte[] mode, byte[] executionCalldata)
        {
            return await _contractService.ExecuteRequestAndWaitForReceiptAsync(mode, executionCalldata);
        }

        public Task<TransactionReceipt> ExecuteAsync(Call call)
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var calldata = ERC7579ExecutionLib.EncodeSingle(call.Target, call.Value, call.Data);
            return ExecuteAsync(mode, calldata);
        }

        public async Task<TransactionReceipt> ExecuteBatchAsync(Call[] calls)
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();
            var calldata = ERC7579ExecutionLib.EncodeBatch(calls);
            return await ExecuteAsync(mode, calldata);
        }

        public async Task<TransactionReceipt> AddDepositAsync(BigInteger amount)
        {
            var function = new AddDepositFunction();
            function.AmountToSend = amount;
            return await _contractService.ContractHandler.SendRequestAndWaitForReceiptAsync(function);
        }

        public async Task<TransactionReceipt> WithdrawDepositToAsync(string to, BigInteger amount)
        {
            return await _contractService.WithdrawDepositToRequestAndWaitForReceiptAsync(to, amount);
        }

        public async Task<bool> IsModuleInstalledAsync(BigInteger moduleTypeId, string moduleAddress, byte[]? additionalContext = null)
        {
            return await _contractService.IsModuleInstalledQueryAsync(moduleTypeId, moduleAddress, additionalContext ?? Array.Empty<byte>());
        }

        public async Task<TransactionReceipt> InstallModuleAsync(BigInteger moduleTypeId, string moduleAddress, byte[] initData)
        {
            return await _contractService.InstallModuleRequestAndWaitForReceiptAsync(moduleTypeId, moduleAddress, initData);
        }

        public async Task<TransactionReceipt> UninstallModuleAsync(BigInteger moduleTypeId, string moduleAddress, byte[] deInitData)
        {
            return await _contractService.UninstallModuleRequestAndWaitForReceiptAsync(moduleTypeId, moduleAddress, deInitData);
        }
    }
}
