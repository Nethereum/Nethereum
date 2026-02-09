using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using NethereumAccountFactoryContractService = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.NethereumAccountFactoryService;

namespace Nethereum.AccountAbstraction.Services
{
    public class SmartAccountFactoryService : ISmartAccountFactory
    {
        private readonly NethereumAccountFactoryContractService _contractService;
        private readonly IWeb3 _web3;
        private string? _entryPointAddress;

        public string Address => _contractService.ContractAddress;
        public string EntryPointAddress => _entryPointAddress ?? throw new InvalidOperationException("EntryPoint not loaded. Call LoadAsync first.");

        public SmartAccountFactoryService(IWeb3 web3, string factoryAddress)
        {
            _web3 = web3;
            _contractService = new NethereumAccountFactoryContractService(web3, factoryAddress);
        }

        public static async Task<SmartAccountFactoryService> LoadAsync(IWeb3 web3, string factoryAddress)
        {
            var service = new SmartAccountFactoryService(web3, factoryAddress);
            await service.LoadEntryPointAsync();
            return service;
        }

        private async Task LoadEntryPointAsync()
        {
            _entryPointAddress = await _contractService.EntryPointQueryAsync();
        }

        public async Task<string> GetAccountAddressAsync(byte[] salt, byte[] initData)
        {
            return await _contractService.GetAddressQueryAsync(salt, initData);
        }

        public async Task<TransactionReceipt> CreateAccountAsync(byte[] salt, byte[] initData)
        {
            return await _contractService.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
        }

        public async Task<bool> IsDeployedAsync(byte[] salt, byte[] initData)
        {
            return await _contractService.IsDeployedQueryAsync(salt, initData);
        }

        public async Task<byte[]> GetInitCodeAsync(byte[] salt, byte[] initData)
        {
            return await _contractService.GetInitCodeQueryAsync(salt, initData);
        }
    }
}
