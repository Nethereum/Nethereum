namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class ContractMapping
    {
        public static void Map(this Contract contract, string contractAddress, string code, Nethereum.RPC.Eth.DTOs.Transaction transaction)
        {
            contract.Address = contractAddress;
            contract.Code = code;
            contract.TransactionHash = transaction.TransactionHash;
            contract.Creator = transaction.From;
        }
    }
}
