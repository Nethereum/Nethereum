using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public class FilterInputBuilder
    {
        public static NewFilterInput GetDefaultFilterInput(string address, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            string[] addresses = null;
            if (!string.IsNullOrEmpty(address))
            {
                addresses = new[] {address};
            }

            return GetDefaultFilterInput(addresses, fromBlock, toBlock);

        }

        public static NewFilterInput GetDefaultFilterInput(string[] addresses, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = new NewFilterInput
            {
                FromBlock = fromBlock ?? BlockParameter.CreateEarliest(),
                ToBlock = toBlock ?? BlockParameter.CreateLatest(),
                Address = addresses
            };
            return ethFilterInput;
        }
    }
}