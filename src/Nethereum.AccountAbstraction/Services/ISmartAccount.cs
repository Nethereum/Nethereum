using System.Numerics;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Services
{
    public interface ISmartAccount
    {
        string Address { get; }
        string EntryPointAddress { get; }

        Task<BigInteger> GetNonceAsync(BigInteger key = default);
        Task<BigInteger> GetDepositAsync();
        Task<bool> IsDeployedAsync();

        Task<TransactionReceipt> ExecuteAsync(byte[] mode, byte[] executionCalldata);
        Task<TransactionReceipt> ExecuteAsync(Call call);
        Task<TransactionReceipt> ExecuteBatchAsync(Call[] calls);

        Task<TransactionReceipt> AddDepositAsync(BigInteger amount);
        Task<TransactionReceipt> WithdrawDepositToAsync(string to, BigInteger amount);

        Task<bool> IsModuleInstalledAsync(BigInteger moduleTypeId, string moduleAddress, byte[]? additionalContext = null);
        Task<TransactionReceipt> InstallModuleAsync(BigInteger moduleTypeId, string moduleAddress, byte[] initData);
        Task<TransactionReceipt> UninstallModuleAsync(BigInteger moduleTypeId, string moduleAddress, byte[] deInitData);
    }
}
