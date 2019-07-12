namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface ITransactionView
    {
        string AddressFrom { get; }
        string AddressTo { get;  }
        string BlockHash { get;  }
        string BlockNumber { get;  }
        string CumulativeGasUsed { get;  }
        string Error { get;  }
        bool Failed { get;  }
        bool FailedCreateContract { get;  }
        string Gas { get;  }
        string GasPrice { get;  }
        string GasUsed { get;  }
        string Hash { get;  }
        bool HasLog { get;  }
        bool HasVmStack { get;  }
        string Input { get;  }
        string NewContractAddress { get;  }
        string Nonce { get;  }
        string ReceiptHash { get;  }
        string TimeStamp { get;  }
        string TransactionIndex { get;  }
        string Value { get;  }
    }
}