using System;
using System.Threading.Tasks;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "PaymasterConfig - static or per-op paymaster data")]
    public class PaymasterConfig
    {
        public string Address { get; set; }
        public byte[] Data { get; set; }
        public Func<UserOperation, Task<byte[]>> DataProvider { get; set; }

        public PaymasterConfig() { }

        public PaymasterConfig(string address, byte[] data = null)
        {
            Address = address;
            Data = data;
        }

        public PaymasterConfig(string address, Func<UserOperation, Task<byte[]>> dataProvider)
        {
            Address = address;
            DataProvider = dataProvider;
        }
    }
}
