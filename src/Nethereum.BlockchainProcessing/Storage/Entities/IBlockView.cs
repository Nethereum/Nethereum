namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface IBlockView
    {
        string BlockNumber { get;  }
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
        string TimeStamp { get; }
    }
}