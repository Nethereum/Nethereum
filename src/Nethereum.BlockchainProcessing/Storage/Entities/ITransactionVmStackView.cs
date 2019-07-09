using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface ITransactionVmStackView
    {
        string Address { get;  }
        string StructLogs { get;  }
        string TransactionHash { get;  }
        JArray GetStructLogs();
    }
}