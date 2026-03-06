namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface ITransactionView
    {
        string AddressFrom { get; }
        string AddressTo { get;  }
        string BlockHash { get;  }
        long BlockNumber { get;  }
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
        long Nonce { get;  }
        string ReceiptHash { get;  }
        long TimeStamp { get;  }
        long TransactionIndex { get;  }
        string Value { get;  }
        string MaxFeePerGas { get; }
        string MaxPriorityFeePerGas { get; }
        long TransactionType { get; }
        string RevertReason { get; }
        string EffectiveGasPrice { get; }
        bool IsCanonical { get; }
        string MaxFeePerBlobGas { get; }
        string BlobGasUsed { get; }
        string BlobGasPrice { get; }
    }
}
