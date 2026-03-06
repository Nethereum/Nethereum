using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.AppChain
{
    public interface IAppChain
    {
        AppChainConfig Config { get; }

        IBlockStore Blocks { get; }
        IStateStore State { get; }
        ITransactionStore Transactions { get; }
        IReceiptStore Receipts { get; }
        ILogStore Logs { get; }
        ITrieNodeStore TrieNodes { get; }

        string WorldAddress { get; }
        string Create2FactoryAddress { get; }

        Task InitializeAsync();
        Task InitializeAsync(GenesisOptions options);
        Task ApplyGenesisStateAsync(GenesisOptions options);

        Task<BigInteger> GetBlockNumberAsync();
        Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber);
        Task<BlockHeader?> GetBlockByHashAsync(byte[] blockHash);
        Task<BlockHeader?> GetLatestBlockAsync();

        Task<BigInteger> GetBalanceAsync(string address);
        Task<BigInteger> GetNonceAsync(string address);
        Task<byte[]?> GetCodeAsync(string address);
        Task<byte[]?> GetStorageAtAsync(string address, BigInteger slot);
        Task<Account?> GetAccountAsync(string address);

        Task<ISignedTransaction?> GetTransactionByHashAsync(byte[] txHash);
        Task<Receipt?> GetTransactionReceiptAsync(byte[] txHash);
    }

    public class GenesisOptions
    {
        public string[]? PrefundedAddresses { get; set; }
        public BigInteger? PrefundBalance { get; set; }
        public bool DeployCreate2Factory { get; set; } = true;
        public string? Create2FactoryAddress { get; set; }
    }
}
