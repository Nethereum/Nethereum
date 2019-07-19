namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface ITransactionLogView
    {
        string Address { get;  }
        string Data { get;  }
        string EventHash { get;  }
        string IndexVal1 { get;  }
        string IndexVal2 { get;  }
        string IndexVal3 { get;  }
        string LogIndex { get;  }
        string TransactionHash { get;  }
    }
}