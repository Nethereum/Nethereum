namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class AddressTransactionMapping
    {
        public static void Map(this AddressTransaction to, Nethereum.RPC.Eth.DTOs.Transaction @from, string address)
        {
            to.BlockNumber = @from.BlockNumber.Value.ToString();
            to.Hash = @from.TransactionHash;
            to.Address = address;
        }
    }
}
