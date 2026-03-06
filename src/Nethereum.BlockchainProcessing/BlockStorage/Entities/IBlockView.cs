namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface IBlockView
    {
        long BlockNumber { get;  }
        string Difficulty { get;  }
        string ExtraData { get;  }
        string GasLimit { get; }
        string GasUsed { get; }
        string Hash { get; }
        string Miner { get; }
        string Nonce { get; }
        string ParentHash { get; }
        string Size { get; }
        string TotalDifficulty { get; }
        long TransactionCount { get; }
        long TimeStamp { get; }
        string BaseFeePerGas { get;}
        string StateRoot { get; }
        string ReceiptsRoot { get; }
        string LogsBloom { get; }
        string WithdrawalsRoot { get; }
        bool IsCanonical { get; }
        bool IsFinalized { get; }
        string BlobGasUsed { get; }
        string ExcessBlobGas { get; }
        string ParentBeaconBlockRoot { get; }
        string RequestsHash { get; }
        string TransactionsRoot { get; }
        string MixHash { get; }
        string Sha3Uncles { get; }
    }
}
