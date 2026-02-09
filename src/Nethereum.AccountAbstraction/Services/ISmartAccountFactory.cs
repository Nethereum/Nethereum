using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Services
{
    public interface ISmartAccountFactory
    {
        string Address { get; }
        string EntryPointAddress { get; }

        Task<string> GetAccountAddressAsync(byte[] salt, byte[] initData);
        Task<TransactionReceipt> CreateAccountAsync(byte[] salt, byte[] initData);
        Task<bool> IsDeployedAsync(byte[] salt, byte[] initData);
        Task<byte[]> GetInitCodeAsync(byte[] salt, byte[] initData);
    }
}
