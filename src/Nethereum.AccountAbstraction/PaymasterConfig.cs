using System;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction
{
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

        public async Task<byte[]> GetPaymasterDataAsync(UserOperation userOperation)
        {
            if (Data != null)
                return Data;

            if (DataProvider != null)
                return await DataProvider(userOperation);

            return Array.Empty<byte>();
        }
    }
}
