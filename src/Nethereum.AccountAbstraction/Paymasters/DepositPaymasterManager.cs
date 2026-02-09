using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Paymasters
{
    public class DepositPaymasterManager : IDepositPaymasterManager
    {
        private readonly DepositPaymasterService _contractService;
        private readonly IWeb3 _web3;
        private string? _entryPointAddress;

        public string Address => _contractService.ContractAddress;
        public string EntryPointAddress => _entryPointAddress ?? throw new InvalidOperationException("EntryPoint not loaded. Call LoadAsync first.");

        public DepositPaymasterManager(IWeb3 web3, string paymasterAddress)
        {
            _web3 = web3;
            _contractService = new DepositPaymasterService(web3, paymasterAddress);
        }

        public static async Task<DepositPaymasterManager> LoadAsync(IWeb3 web3, string paymasterAddress)
        {
            var service = new DepositPaymasterManager(web3, paymasterAddress);
            await service.LoadEntryPointAsync();
            return service;
        }

        private async Task LoadEntryPointAsync()
        {
            _entryPointAddress = await _contractService.EntryPointQueryAsync();
        }

        public Task<SponsorResult> SponsorUserOperationAsync(PackedUserOperation userOp, SponsorContext? context = null)
        {
            var paymasterAndData = Address.HexToByteArray();
            return Task.FromResult(SponsorResult.Success(paymasterAndData, Address));
        }

        public async Task<BigInteger> GetDepositAsync()
        {
            return await _contractService.GetDepositQueryAsync();
        }

        public async Task<TransactionReceipt> DepositAsync(BigInteger amount)
        {
            var function = new DepositFunction();
            function.AmountToSend = amount;
            return await _contractService.ContractHandler.SendRequestAndWaitForReceiptAsync(function);
        }

        public async Task<TransactionReceipt> WithdrawToAsync(string to, BigInteger amount)
        {
            return await _contractService.WithdrawToRequestAndWaitForReceiptAsync(to, amount);
        }

        public async Task<BigInteger> GetUserDepositAsync(string account)
        {
            return await _contractService.DepositsQueryAsync(account);
        }

        public async Task<TransactionReceipt> DepositForAsync(string account, BigInteger amount)
        {
            var function = new DepositForFunction { Account = account };
            function.AmountToSend = amount;
            return await _contractService.ContractHandler.SendRequestAndWaitForReceiptAsync(function);
        }

        public async Task<TransactionReceipt> WithdrawFromAsync(string account, BigInteger amount)
        {
            return await _contractService.WithdrawToRequestAndWaitForReceiptAsync(account, amount);
        }
    }
}
