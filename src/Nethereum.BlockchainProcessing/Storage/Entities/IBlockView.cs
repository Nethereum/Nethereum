namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface IBlockView
    {
        string BlockNumber { get;  }
        long Difficulty { get;  }
        string ExtraData { get;  }
        long GasLimit { get; }
        long GasUsed { get; }
        string Hash { get; }
        string Miner { get; }
        long Nonce { get; }
        string ParentHash { get; }
        long Size { get; }
        long TotalDifficulty { get; }
        long TransactionCount { get; }
        long TimeStamp { get; }
    }
}