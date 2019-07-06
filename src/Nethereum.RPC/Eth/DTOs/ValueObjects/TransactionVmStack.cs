using System;
using System.Linq;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Eth.DTOs
{
    //public class TransactionVmStack
    //{
    //    public TransactionVmStack(string transactionHash, string address, JObject stackTrace)
    //    {
    //        TransactionHash = transactionHash;
    //        Address = address;
    //        StackTrace = stackTrace;
    //    }

    //    public string TransactionHash { get; private set; }
    //    public string Address { get; private set; }
    //    public JObject StackTrace { get; private set; }

    //    public StructLog[] GetInterContractCalls()
    //    {
    //        return StackTrace?.GetInterContractCalls(Address).ToArray() ?? Array.Empty<StructLog>();
    //    }
    //}
}