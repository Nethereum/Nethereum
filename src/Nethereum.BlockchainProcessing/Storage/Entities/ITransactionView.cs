namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface ITransactionView
    {
        string AddressFrom { get; }
        string AddressTo { get;  }
        string BlockHash { get;  }
        string BlockNumber { get;  }
        long CumulativeGasUsed { get;  }
        string Error { get;  }
        bool Failed { get;  }
        bool FailedCreateContract { get;  }
        long Gas { get;  }
        long GasPrice { get;  }
        long GasUsed { get;  }
        string Hash { get;  }
        bool HasLog { get;  }
        bool HasVmStack { get;  }
        string Input { get;  }
        string NewContractAddress { get;  }
        long Nonce { get;  }
        string ReceiptHash { get;  }
        long TimeStamp { get;  }
        long TransactionIndex { get;  }
        string Value { get;  }
    }
}